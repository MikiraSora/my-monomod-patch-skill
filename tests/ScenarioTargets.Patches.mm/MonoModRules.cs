using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.InlineRT;

namespace MonoMod;

/// <summary>
/// Patch-time rules that create dnSpy-visible marker infrastructure in the target
/// module and register a PostProcessor to perform precise IL insertion between
/// existing call instructions. See modifier-recipes.md "Precise IL Insertion".
/// </summary>
public class MonoModRules
{
    static MonoModRules()
    {
        var modder = MonoModRulesManager.Modder;
        var module = modder.Module;

        CreateMarkerInfrastructure(module);

        // Defer all IL insertion to PostProcessor (runs after patch_ members copied).
        modder.PostProcessors += PostProcess;
    }

    /// <summary>
    /// Create the PatchInsertMarkerAttribute type and the __PatchMarker no-op
    /// static method in the target module so they survive into patched output.
    /// </summary>
    private static void CreateMarkerInfrastructure(ModuleDefinition module)
    {
        var voidRef = module.TypeSystem.Void;
        var stringRef = module.TypeSystem.String;

        // PatchInsertMarkerAttribute
        var attrType = new TypeDefinition("MonoMod", "PatchInsertMarkerAttribute",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            module.ImportReference(typeof(Attribute)));
        var attrCtor = new MethodDefinition(".ctor",
            MethodAttributes.Public | MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName | MethodAttributes.HideBySig, voidRef);
        var ctorBody = new MethodBody(attrCtor);
        ctorBody.InitLocals = true;
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Call,
            module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes))));
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Ret));
        attrCtor.Body = ctorBody;
        attrType.Methods.Add(attrCtor);
        module.Types.Add(attrType);

        // PatchMarkers.__PatchMarker(string)
        var markerType = new TypeDefinition("MonoMod", "PatchMarkers",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        var markerMethod = new MethodDefinition("__PatchMarker",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, voidRef);
        markerMethod.Parameters.Add(new ParameterDefinition("tag", ParameterAttributes.None, stringRef));
        var markerBody = new MethodBody(markerMethod);
        markerBody.InitLocals = true;
        markerBody.Instructions.Add(Instruction.Create(OpCodes.Ret));
        markerMethod.Body = markerBody;
        markerType.Methods.Add(markerMethod);
        module.Types.Add(markerType);
    }

    private static void PostProcess(MonoModder modder)
    {
        var module = modder.Module;
        var markerAttrCtor = module.GetType("MonoMod.PatchInsertMarkerAttribute")
            .Methods.First(m => m.Name == ".ctor");
        var markerMethod = module.GetType("MonoMod.PatchMarkers")
            .Methods.First(m => m.Name == "__PatchMarker");

        // ---- S200-S203: basic middle-insert scenarios ----
        InsertMiddleCall(module,
            "MonoModTestTargets.S200_MiddleInsertVoidCall", "Run",
            anchorCallName: "First", insertCallName: "Second",
            tag: "S200_InsertSecond", markerMethod, markerAttrCtor);

        InsertMiddleCall(module,
            "MonoModTestTargets.S201_MiddleInsertWithReturn", "Process",
            anchorCallName: "Compute", insertCallName: "LogComputed",
            tag: "S201_InsertLogComputed", markerMethod, markerAttrCtor);

        InsertMiddleCall(module,
            "MonoModTestTargets.S202_MiddleInsertMarker", "Step",
            anchorCallName: "Begin", insertCallName: "Middle",
            tag: "S202_InsertMiddle", markerMethod, markerAttrCtor);

        InsertMiddleCall(module,
            "MonoModTestTargets.S203_MiddleInsertInTry", "SafeRun",
            anchorCallName: "A", insertCallName: "B",
            tag: "S203_InsertBInTry", markerMethod, markerAttrCtor);

        // ---- S210-S215: advanced middle-insert scenarios ----

        // S210: insert Tick() inside the for-loop, before Log.Append(i).
        // Anchor on the call to get_Log (the property getter used by Log.Append),
        // and insert before it. This puts Tick() at the start of each loop iteration.
        InsertBeforeCall(module,
            "MonoModTestTargets.S210_LoopBodyInsert", "Run",
            targetCallName: "get_Log", insertCallName: "Tick",
            tag: "S210_InsertTickInLoop", markerMethod, markerAttrCtor,
            occurrence: 1);  // first (and only) get_Log call is inside the loop

        // S211: insert LogValue(Counter) between Begin() and End().
        // The inserted method takes one int parameter: load Counter property first.
        InsertCallWithArg(module,
            "MonoModTestTargets.S211_ParamInsert", "Run",
            anchorCallName: "Begin", insertCallName: "LogValue",
            argProviderCallName: "get_Counter",
            tag: "S211_InsertLogValue", markerMethod, markerAttrCtor);

        // S212: insert PostProcess() after virtual GetName() (callvirt) + stloc.
        InsertMiddleCall(module,
            "MonoModTestTargets.S212_VirtualChainInsert", "Build",
            anchorCallName: "GetName", insertCallName: "PostProcess",
            tag: "S212_InsertAfterVirtual", markerMethod, markerAttrCtor);

        // S213: marker-only insertion between GetPrefix() return and string concat.
        // The return value is consumed directly on the stack, so we can only insert
        // a stack-neutral marker (ldstr + call __PatchMarker), not a method call.
        InsertMarkerOnly(module,
            "MonoModTestTargets.S213_StackConsumeInsert", "Build",
            anchorCallName: "GetPrefix",
            tag: "S213_MarkerOnlyStackNeutral", markerMethod, markerAttrCtor);

        // S214: insert HandleCatch() inside catch block, before Log.Append("caught").
        InsertBeforeCall(module,
            "MonoModTestTargets.S214_CatchBlockInsert", "SafeExec",
            targetCallName: "get_Log", insertCallName: "HandleCatch",
            tag: "S214_InsertInCatch", markerMethod, markerAttrCtor,
            occurrence: 1);  // first (and only) get_Log is inside the catch block

        // S215: insert static StepB() between static StepA() and StepC().
        // No ldarg_0; use Call instead of Callvirt.
        InsertStaticCall(module,
            "MonoModTestTargets.S215_StaticInsert", "RunStatic",
            anchorCallName: "StepA", insertCallName: "StepB",
            tag: "S215_InsertStaticMethod", markerMethod, markerAttrCtor);

        // ---- S220-S225: advanced complex insertion scenarios ----

        // S220: insert LogMid() after first Items.Add(1) call and before second.
        // Anchor on the callvirt List<int>.Add (occurrence 1).
        InsertMiddleCall(module,
            "MonoModTestTargets.S220_GenericMethodInsert", "Populate",
            anchorCallName: "Add", insertCallName: "LogMid",
            tag: "S220_InsertAfterFirstAdd", markerMethod, markerAttrCtor);

        // S221: insert HandleInnerCatch() inside inner catch, before get_Log.
        // The first get_Log call (occurrence 1) is in the inner catch block.
        InsertBeforeCall(module,
            "MonoModTestTargets.S221_NestedTryCatchInsert", "Run",
            targetCallName: "get_Log", insertCallName: "HandleInnerCatch",
            tag: "S221_InsertInInnerCatch", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S222: insert CaseBExtra() after CaseB() in switch case 2 branch.
        InsertMiddleCall(module,
            "MonoModTestTargets.S222_SwitchBranchInsert", "Classify",
            anchorCallName: "CaseB", insertCallName: "CaseBExtra",
            tag: "S222_InsertInCaseB", markerMethod, markerAttrCtor);

        // S223: insert LogMid() between first Bump and second Bump.
        InsertMiddleCall(module,
            "MonoModTestTargets.S223_RefParamInsert", "Process",
            anchorCallName: "Bump", insertCallName: "LogMid",
            tag: "S223_InsertBetweenBumps", markerMethod, markerAttrCtor);

        // S224: insert AfterMarkEarly() after MarkEarly() in early-return branch.
        InsertMiddleCall(module,
            "MonoModTestTargets.S224_MultiReturnInsert", "Evaluate",
            anchorCallName: "MarkEarly", insertCallName: "AfterMarkEarly",
            tag: "S224_InsertAfterMarkEarly", markerMethod, markerAttrCtor);

        // S225: insert LogDimensions(Width, Height) between Start() and End().
        // Multi-param: ldarg_0 (this for LogDimensions), ldarg_0+callvirt get_Width,
        // ldarg_0+callvirt get_Height, callvirt LogDimensions.
        InsertCallWithTwoArgs(module,
            "MonoModTestTargets.S225_MultiParamInsert", "Run",
            anchorCallName: "Start", insertCallName: "LogDimensions",
            argProviderCallName1: "get_Width", argProviderCallName2: "get_Height",
            tag: "S225_InsertMultiParam", markerMethod, markerAttrCtor);

        // ---- S226-S231: try-finally / multi-insert / chain / lock / using / before-ret ----

        // S226: insert MarkFinally() in finally block, before Cleanup().
        InsertBeforeCall(module,
            "MonoModTestTargets.S226_TryFinallyInsert", "Run",
            targetCallName: "Cleanup", insertCallName: "MarkFinally",
            tag: "S226_InsertInFinally", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S227: two insertions in same method - after Alpha() and after Beta().
        InsertMiddleCall(module,
            "MonoModTestTargets.S227_MultiInsertSameMethod", "Run",
            anchorCallName: "Alpha", insertCallName: "AfterAlpha",
            tag: "S227_InsertAfterAlpha", markerMethod, markerAttrCtor);
        InsertMiddleCall(module,
            "MonoModTestTargets.S227_MultiInsertSameMethod", "Run",
            anchorCallName: "Beta", insertCallName: "AfterBeta",
            tag: "S227_InsertAfterBeta", markerMethod, markerAttrCtor);

        // S228: insert PostChain() after Done() (the whole Self().Done() chain).
        InsertMiddleCall(module,
            "MonoModTestTargets.S228_ChainedCallInsert", "Run",
            anchorCallName: "Done", insertCallName: "PostChain",
            tag: "S228_InsertAfterChain", markerMethod, markerAttrCtor);

        // S229: insert PreLocked() inside lock body, before Locked().
        InsertBeforeCall(module,
            "MonoModTestTargets.S229_LockBodyInsert", "Run",
            targetCallName: "Locked", insertCallName: "PreLocked",
            tag: "S229_InsertInLock", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S230: insert PreInner() inside using body, before Inner().
        InsertBeforeCall(module,
            "MonoModTestTargets.S230_UsingBodyInsert", "Run",
            targetCallName: "Inner", insertCallName: "PreInner",
            tag: "S230_InsertInUsing", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S231: insert BeforeReturn() after First(), before ret.
        InsertMiddleCall(module,
            "MonoModTestTargets.S231_BeforeRetInsert", "Run",
            anchorCallName: "First", insertCallName: "BeforeReturn",
            tag: "S231_InsertBeforeRet", markerMethod, markerAttrCtor);

        // ---- S232-S237: cross-type / enum / loops / boxed / string-const ----

        // S232: insert CrossNote() between Begin() and End(). CrossNote internally
        // calls a method on a different type (S232_CrossTypeHelper).
        InsertMiddleCall(module,
            "MonoModTestTargets.S232_CrossTypeInsert", "Run",
            anchorCallName: "Begin", insertCallName: "CrossNote",
            tag: "S232_InsertCrossType", markerMethod, markerAttrCtor);

        // S233: insert LogLevel(Current) between Start() and Stop().
        // Enum argument loaded from property getter (works like int at IL level).
        InsertCallWithArg(module,
            "MonoModTestTargets.S233_EnumArgInsert", "Run",
            anchorCallName: "Start", insertCallName: "LogLevel",
            argProviderCallName: "get_Current",
            tag: "S233_InsertEnumArg", markerMethod, markerAttrCtor);

        // S234: insert PreTick() before Tick() inside do-while body.
        InsertBeforeCall(module,
            "MonoModTestTargets.S234_DoWhileInsert", "Run",
            targetCallName: "Tick", insertCallName: "PreTick",
            tag: "S234_InsertInDoWhile", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S235: insert PreStep() before Step() inside while body.
        InsertBeforeCall(module,
            "MonoModTestTargets.S235_WhileInsert", "Run",
            targetCallName: "Step", insertCallName: "PreStep",
            tag: "S235_InsertInWhile", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S236: insert LogBoxed(BoxedValue) between First() and Last().
        // The int property value must be boxed to object before passing.
        InsertCallWithBoxedPropertyArg(module,
            "MonoModTestTargets.S236_BoxedValueInsert", "Run",
            anchorCallName: "First", insertCallName: "LogBoxed",
            argProviderCallName: "get_BoxedValue",
            tag: "S236_InsertBoxedArg", markerMethod, markerAttrCtor);

        // S237: insert LogTag("mid") between Alpha() and Omega().
        // String constant argument loaded via ldstr.
        InsertCallWithStringConstArg(module,
            "MonoModTestTargets.S237_StringArgInsert", "Run",
            anchorCallName: "Alpha", insertCallName: "LogTag",
            stringArg: "mid",
            tag: "S237_InsertStringConstArg", markerMethod, markerAttrCtor);

        // ---- S238-S243: local func / switch expr / ternary / checked / goto / params ----

        // S238: insert MidNote() after Before(), before LocalSquare call.
        InsertMiddleCall(module,
            "MonoModTestTargets.S238_LocalFuncInsert", "Run",
            anchorCallName: "Before", insertCallName: "MidNote",
            tag: "S238_InsertAfterBefore", markerMethod, markerAttrCtor);

        // S239: insert MidNote() after Start(), before Classify call.
        InsertMiddleCall(module,
            "MonoModTestTargets.S239_SwitchExprInsert", "Run",
            anchorCallName: "Start", insertCallName: "MidNote",
            tag: "S239_InsertAfterStart", markerMethod, markerAttrCtor);

        // S240: insert MidNote() before the second get_Log call (after ternary stloc).
        InsertBeforeCall(module,
            "MonoModTestTargets.S240_TernaryInsert", "Run",
            targetCallName: "get_Log", insertCallName: "MidNote",
            tag: "S240_InsertAfterTernary", markerMethod, markerAttrCtor,
            occurrence: 2);

        // S241: insert MidNote() after First(), in checked block.
        InsertMiddleCall(module,
            "MonoModTestTargets.S241_CheckedInsert", "Run",
            anchorCallName: "First", insertCallName: "MidNote",
            tag: "S241_InsertInChecked", markerMethod, markerAttrCtor);

        // S242: insert MidNote() after Enter(), before goto loop.
        InsertMiddleCall(module,
            "MonoModTestTargets.S242_GotoFlowInsert", "Run",
            anchorCallName: "Enter", insertCallName: "MidNote",
            tag: "S242_InsertAfterEnter", markerMethod, markerAttrCtor);

        // S243: insert MidNote() after First(), before Build call.
        InsertMiddleCall(module,
            "MonoModTestTargets.S243_ParamsArrayInsert", "Run",
            anchorCallName: "First", insertCallName: "MidNote",
            tag: "S243_InsertAfterFirst", markerMethod, markerAttrCtor);

        // ---- S244-S250: nullable / ref struct / exception filter / nested try / recursive / static / index ----

        // S244: insert MidNote() after First(), before TryGetName.
        InsertMiddleCall(module,
            "MonoModTestTargets.S244_NullableReturnInsert", "Run",
            anchorCallName: "First", insertCallName: "MidNote",
            tag: "S244_InsertAfterFirst", markerMethod, markerAttrCtor);

        // S245: insert MidNote() after First(), before SumSpan.
        InsertMiddleCall(module,
            "MonoModTestTargets.S245_RefStructInsert", "Run",
            anchorCallName: "First", insertCallName: "MidNote",
            tag: "S245_InsertAfterFirst", markerMethod, markerAttrCtor);

        // S246: insert PreHandle() in catch-with-filter block, before HandleError().
        InsertBeforeCall(module,
            "MonoModTestTargets.S246_ExceptionFilterInsert", "SafeExec",
            targetCallName: "HandleError", insertCallName: "PreHandle",
            tag: "S246_InsertInFilteredCatch", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S247: insert MidNote() after StepA(), before inner try block.
        InsertMiddleCall(module,
            "MonoModTestTargets.S247_NestedTryInTryInsert", "Run",
            anchorCallName: "StepA", insertCallName: "MidNote",
            tag: "S247_InsertAfterStepA", markerMethod, markerAttrCtor);

        // S248: insert PreRecurse() before recursive Factorial() call.
        InsertBeforeCall(module,
            "MonoModTestTargets.S248_RecursiveInsert", "Factorial",
            targetCallName: "Factorial", insertCallName: "PreRecurse",
            tag: "S248_InsertBeforeRecurse", markerMethod, markerAttrCtor,
            occurrence: 1);

        // S249: insert static MidNote() after Append("init;"), before set_Tag.
        // Anchor on the Append callvirt, skip pop, then insert static call.
        InsertStaticCall(module,
            "MonoModTestTargets.S249_StaticCtorContext", "Init",
            anchorCallName: "Append", insertCallName: "MidNote",
            tag: "S249_InsertStaticInInit", markerMethod, markerAttrCtor);

        // S250: insert MidNote() after First(), before Data[1] access.
        InsertMiddleCall(module,
            "MonoModTestTargets.S250_IndexAccessInsert", "Run",
            anchorCallName: "First", insertCallName: "MidNote",
            tag: "S250_InsertAfterFirst", markerMethod, markerAttrCtor);
    }

    // -----------------------------------------------------------------------
    // Insertion helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Find the call to anchorCallName, skip past pop/stloc, then insert
    /// ldstr; call __PatchMarker; ldarg_0; callvirt insertMethod  after it.
    /// </summary>
    private static void InsertMiddleCall(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string tag,
        MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            var seq = new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Callvirt, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert before the Nth occurrence of a call instruction (by callee name).
    /// Used when the insertion point is inside a loop or catch block.
    /// </summary>
    private static void InsertBeforeCall(
        ModuleDefinition module, string typeName, string methodName,
        string targetCallName, string insertCallName, string tag,
        MethodDefinition markerMethod, MethodReference markerAttrCtor,
        int occurrence)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var target = FindCallByName(method, targetCallName, occurrence);
        if (target == null) return;

        var il = method.Body.GetILProcessor();
        // InsertBefore inserts a single instruction before the target.
        // Insert in forward order for InsertBefore (reverse of InsertAfter).
        var ldstrTag   = il.Create(OpCodes.Ldstr, tag);
        var callMarker = il.Create(OpCodes.Call, markerMethod);
        var ldarg0     = il.Create(OpCodes.Ldarg_0);
        var callInsert = il.Create(OpCodes.Callvirt, insertMethod);

        il.InsertBefore(target, ldstrTag);
        il.InsertBefore(target, callMarker);
        il.InsertBefore(target, ldarg0);
        il.InsertBefore(target, callInsert);

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert a call with one argument: load the argument via a property getter
    /// call, then call the insert method with it.
    /// </summary>
    private static void InsertCallWithArg(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string argProviderCallName,
        string tag, MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        // Find the property getter to load the argument value.
        var argGetter = type.Methods.FirstOrDefault(m => m.Name == argProviderCallName);
        if (argGetter == null) return;

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            // Stack order for instance method with one arg:
            //   ldarg_0 (this for insertMethod)
            //   ldarg_0 (this for argGetter)
            //   callvirt argGetter  -> pushes the argument value
            //   callvirt insertMethod(this, arg)
            var seq = new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Callvirt, argGetter),
                il.Create(OpCodes.Callvirt, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert an instance method call with two arguments loaded from property
    /// getters. Stack sequence:
    ///   ldarg_0 (this for insertMethod)
    ///   ldarg_0 (this for arg1 getter) -> callvirt getter1 -> pushes arg1
    ///   ldarg_0 (this for arg2 getter) -> callvirt getter2 -> pushes arg2
    ///   callvirt insertMethod(this, arg1, arg2)
    /// </summary>
    private static void InsertCallWithTwoArgs(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName,
        string argProviderCallName1, string argProviderCallName2,
        string tag, MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        var argGetter1 = type.Methods.FirstOrDefault(m => m.Name == argProviderCallName1);
        var argGetter2 = type.Methods.FirstOrDefault(m => m.Name == argProviderCallName2);
        if (argGetter1 == null || argGetter2 == null) return;

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            var seq = new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Callvirt, argGetter1),
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Callvirt, argGetter2),
                il.Create(OpCodes.Callvirt, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert only a marker call (ldstr + call __PatchMarker) without any
    /// method call. Used when the stack has an unconsumed return value that
    /// must not be disturbed.
    /// </summary>
    private static void InsertMarkerOnly(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string tag,
        MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var type = module.GetType(typeName);
        if (type == null) return;
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method?.Body == null) return;

        // The anchor's return value is on the stack and consumed by the next
        // instruction. We insert the marker BEFORE the next instruction that
        // consumes it, not after the anchor.
        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;

        var instrs = method.Body.Instructions;
        var idx = instrs.IndexOf(anchor) + 1;
        if (idx >= instrs.Count) return;
        // The next instruction consumes the return value (e.g., it's part of
        // a string.Concat call sequence). Insert the marker before it.
        // But we can't insert between the anchor and its consumer because the
        // consumer expects the value on the stack. Instead, insert the marker
        // BEFORE the anchor itself (the anchor is a call that pushes a value;
        // before it the stack is empty relative to this expression).
        // Actually: the marker is stack-neutral, so it's safe to insert anywhere
        // the stack is empty. Insert before the anchor call.
        var il = method.Body.GetILProcessor();
        var ldstrTag   = il.Create(OpCodes.Ldstr, tag);
        var callMarker = il.Create(OpCodes.Call, markerMethod);

        il.InsertBefore(anchor, ldstrTag);
        il.InsertBefore(anchor, callMarker);

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert a static method call (no ldarg_0, use Call not Callvirt).
    /// </summary>
    private static void InsertStaticCall(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string tag,
        MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            var seq = new[]
            {
                il.Create(OpCodes.Call, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert an instance method call with a boxed property argument.
    /// Stack: ldarg_0 (this), ldarg_0 (this for getter), callvirt getter,
    /// box T, callvirt insertMethod(this, boxed).
    /// </summary>
    private static void InsertCallWithBoxedPropertyArg(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string argProviderCallName,
        string tag, MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        var argGetter = type.Methods.FirstOrDefault(m => m.Name == argProviderCallName);
        if (argGetter == null) return;

        // The property's return type (e.g. int32) needs to be boxed to object.
        var propReturnType = argGetter.ReturnType;

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            var seq = new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Callvirt, argGetter),
                il.Create(OpCodes.Box, propReturnType),
                il.Create(OpCodes.Callvirt, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    /// <summary>
    /// Insert an instance method call with a string constant argument.
    /// Stack: ldarg_0 (this), ldstr value, callvirt insertMethod(this, string).
    /// </summary>
    private static void InsertCallWithStringConstArg(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string stringArg,
        string tag, MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var (type, method, insertMethod) = ResolveInsertTargets(
            module, typeName, methodName, insertCallName);
        if (method == null || insertMethod == null) return;

        var anchor = FindCallByName(method, anchorCallName);
        if (anchor == null) return;
        anchor = SkipReturnValueConsumer(method, anchor);

        var il = method.Body.GetILProcessor();
        EmitInsertionAfter(il, anchor, tag, markerMethod, () =>
        {
            var seq = new[]
            {
                il.Create(OpCodes.Ldarg_0),
                il.Create(OpCodes.Ldstr, stringArg),
                il.Create(OpCodes.Callvirt, insertMethod)
            };
            return seq;
        });

        AddMethodMarker(method, markerAttrCtor);
    }

    // -----------------------------------------------------------------------
    // Shared low-level helpers
    // -----------------------------------------------------------------------

    private static (TypeDefinition type, MethodDefinition method, MethodDefinition insertMethod)
        ResolveInsertTargets(ModuleDefinition module, string typeName, string methodName, string insertCallName)
    {
        var type = module.GetType(typeName);
        if (type == null) return (null, null, null);
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method?.Body == null) return (null, null, null);
        var insertMethod = type.Methods.FirstOrDefault(m => m.Name == insertCallName);
        return (type, method, insertMethod);
    }

    /// <summary>
    /// Find the Nth call/callvirt instruction whose callee matches by name.
    /// occurrence=1 (default) returns the first match.
    /// </summary>
    private static Instruction FindCallByName(MethodDefinition method, string calleeName, int occurrence = 1)
    {
        int found = 0;
        foreach (var ins in method.Body.Instructions)
        {
            if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt) &&
                ins.Operand is MethodReference mr && mr.Name == calleeName)
            {
                found++;
                if (found == occurrence) return ins;
            }
        }
        return null;
    }

    /// <summary>
    /// If the instruction after anchor is pop/stloc (consuming a non-void
    /// return value), return that consumer so insertion happens after it.
    /// </summary>
    private static Instruction SkipReturnValueConsumer(MethodDefinition method, Instruction anchor)
    {
        var instrs = method.Body.Instructions;
        var idx = instrs.IndexOf(anchor) + 1;
        if (idx < instrs.Count)
        {
            var next = instrs[idx];
            if (next.OpCode == OpCodes.Pop ||
                next.OpCode == OpCodes.Stloc_0 || next.OpCode == OpCodes.Stloc_1 ||
                next.OpCode == OpCodes.Stloc_2 || next.OpCode == OpCodes.Stloc_3 ||
                next.OpCode == OpCodes.Stloc_S || next.OpCode == OpCodes.Stloc)
            {
                return next;
            }
        }
        return anchor;
    }

    /// <summary>
    /// Insert a marker call (ldstr + call __PatchMarker) followed by a
    /// caller-provided instruction sequence, all after the anchor instruction.
    /// InsertAfter chains from anchor, so insert in reverse order.
    /// </summary>
    private static void EmitInsertionAfter(
        ILProcessor il, Instruction anchor, string tag,
        MethodDefinition markerMethod, Func<Instruction[]> buildCallSeq)
    {
        var callSeq = buildCallSeq();

        // Insert in reverse: last instruction first, so order is preserved.
        for (int i = callSeq.Length - 1; i >= 0; i--)
            il.InsertAfter(anchor, callSeq[i]);

        var callMarker = il.Create(OpCodes.Call, markerMethod);
        var ldstrTag   = il.Create(OpCodes.Ldstr, tag);
        il.InsertAfter(anchor, callMarker);
        il.InsertAfter(anchor, ldstrTag);
    }

    private static void AddMethodMarker(MethodDefinition method, MethodReference markerAttrCtor)
    {
        method.CustomAttributes.Add(new CustomAttribute(markerAttrCtor));
    }
}