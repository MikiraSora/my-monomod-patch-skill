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

        // 1. Create PatchInsertMarkerAttribute type in target module.
        var voidRef = module.TypeSystem.Void;
        var stringRef = module.TypeSystem.String;
        var attrBase = module.ImportReference(typeof(Attribute));

        var attrType = new TypeDefinition("MonoMod", "PatchInsertMarkerAttribute",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            attrBase);
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

        // 2. Create PatchMarkers type with static no-op __PatchMarker(string) method.
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

        // 3. Defer all IL insertion to PostProcessor (runs after patch_ members copied).
        modder.PostProcessors += PostProcess;
    }

    private static void PostProcess(MonoModder modder)
    {
        var module = modder.Module;
        var markerAttrType = module.GetType("MonoMod.PatchInsertMarkerAttribute");
        var markerAttrCtor = markerAttrType.Methods.First(m => m.Name == ".ctor");
        var markerMethod = module.GetType("MonoMod.PatchMarkers").Methods.First(m => m.Name == "__PatchMarker");

        // S200: insert Second() between First() and Third() in Run().
        InsertMiddleCall(module,
            "MonoModTestTargets.S200_MiddleInsertVoidCall", "Run",
            anchorCallName: "First", insertCallName: "Second",
            tag: "S200_InsertSecond", markerMethod, markerAttrCtor);

        // S201: insert LogComputed() after Compute() (skipping stloc) before Done().
        InsertMiddleCall(module,
            "MonoModTestTargets.S201_MiddleInsertWithReturn", "Process",
            anchorCallName: "Compute", insertCallName: "LogComputed",
            tag: "S201_InsertLogComputed", markerMethod, markerAttrCtor);

        // S202: insert Middle() between Begin() and End() in Step().
        InsertMiddleCall(module,
            "MonoModTestTargets.S202_MiddleInsertMarker", "Step",
            anchorCallName: "Begin", insertCallName: "Middle",
            tag: "S202_InsertMiddle", markerMethod, markerAttrCtor);

        // S203: insert B() between A() and C() inside try block of SafeRun().
        InsertMiddleCall(module,
            "MonoModTestTargets.S203_MiddleInsertInTry", "SafeRun",
            anchorCallName: "A", insertCallName: "B",
            tag: "S203_InsertBInTry", markerMethod, markerAttrCtor);
    }

    private static void InsertMiddleCall(
        ModuleDefinition module, string typeName, string methodName,
        string anchorCallName, string insertCallName, string tag,
        MethodDefinition markerMethod, MethodReference markerAttrCtor)
    {
        var type = module.GetType(typeName);
        if (type == null) return;
        var method = type.Methods.FirstOrDefault(m => m.Name == methodName);
        if (method?.Body == null) return;
        var insertMethod = type.Methods.FirstOrDefault(m => m.Name == insertCallName);
        if (insertMethod == null) return;

        var il = method.Body.GetILProcessor();
        var instrs = method.Body.Instructions;

        // Find anchor call by callee method name.
        Instruction anchor = null;
        foreach (var ins in instrs)
        {
            if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt) &&
                ins.Operand is MethodReference mr && mr.Name == anchorCallName)
            {
                anchor = ins;
                break;
            }
        }
        if (anchor == null) return;

        // Skip past pop/stloc if the anchor method returns non-void.
        var idx = instrs.IndexOf(anchor) + 1;
        if (idx < instrs.Count)
        {
            var next = instrs[idx];
            if (next.OpCode == OpCodes.Pop ||
                next.OpCode == OpCodes.Stloc_0 || next.OpCode == OpCodes.Stloc_1 ||
                next.OpCode == OpCodes.Stloc_2 || next.OpCode == OpCodes.Stloc_3 ||
                next.OpCode == OpCodes.Stloc_S || next.OpCode == OpCodes.Stloc)
            {
                anchor = next;
            }
        }

        // Insert marker + call in reverse order (InsertAfter chains from anchor).
        var ldstrTag   = il.Create(OpCodes.Ldstr, tag);
        var callMarker = il.Create(OpCodes.Call, markerMethod);
        var ldarg0     = il.Create(OpCodes.Ldarg_0);
        var callInsert = il.Create(OpCodes.Callvirt, insertMethod);

        il.InsertAfter(anchor, callInsert);
        il.InsertAfter(anchor, ldarg0);
        il.InsertAfter(anchor, callMarker);
        il.InsertAfter(anchor, ldstrTag);

        // Method-level marker attribute.
        method.CustomAttributes.Add(new CustomAttribute(markerAttrCtor));
    }
}