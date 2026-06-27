# Optional MonoMod Modifier Recipes

Use this reference only when the user explicitly asks for modifiers beyond `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor`.

## Replacement And Removal

- `[MonoModReplace]`: replace existing behavior without generating an `orig_` copy. Use for full replacement when calling original code would be wrong.
- `[MonoModRemove]`: remove a target type/member. Treat as destructive; require a clear user request or test case.
- `[MonoModPublic]`: make a target type/member public during post-processing.

## Original Naming

- `[MonoModOriginalName("orig_CustomName")]`: override the generated original-method name used for a patch method.
- `[MonoModOriginal]`: mark a method as an original stub even if it does not use the `orig_` prefix.

## Conditional And Multi-Target Patches

- `[MonoModTargetModule("AssemblyName")]`: apply only to a specific target module or full assembly name.
- `[MonoModIfFlag("flagName", fallback)]`: apply based on `MonoModder.SharedData`, usually set from `MonoModRules`.
- `[MonoModNoNew]`: on methods, skip if the target method does not exist. Current MonoMod field handling skips fields marked with this attribute.
- `[MonoModOnPlatform(...)]`: intended for platform filtering. Verify behavior in the exact MonoMod version before relying on it.

## Relinking

- `[MonoModLinkFrom("FindableID")]`: relink references from another target to the annotated type/member.
- `[MonoModLinkTo(...)]`: relink references to the annotated item toward another target.
- `[MonoModHook]`: obsolete. Prefer `MonoModLinkFrom` for static relinking or RuntimeDetour for runtime hooks.
- `[MonoModForceCall]` and `[MonoModForceCallvirt]`: force relinked calls to use IL `call` or `callvirt`.

## Custom Rules

`MonoMod.MonoModRules` can register custom attribute handlers and perform Cecil-level changes at patch time. Use it only when normal patch classes and modifiers cannot express the change.

Do not make MonoModRules the default approach. It increases fragility and requires Mono.Cecil knowledge.

## Precise IL Insertion (Middle-Of-Method Patching)

Use this recipe when the patch must insert code **between** two existing instructions inside a method body, not just wrap the whole method with `orig_`. The standard `orig_` wrapper treats the original method as an indivisible black box, so it cannot express "insert B between A and C".

### When To Use

- The target method calls A then C, and you need to insert B between them.
- The insertion point must be precise (between two specific call instructions), not just "before" or "after" the whole method.
- `MonoModReplace` (full rewrite) is not viable because A or C is too complex to re-express by hand.

### Critical: Static Constructor

MonoModRulesManager.ExecuteRules calls RuntimeHelpers.RunClassConstructor on the rules type, which triggers the **static** constructor (.cctor), not the instance constructor. The rules class must use static MonoModRules() { ... }, not public MonoModRules() { ... }.

### Mechanism

MonoMod executes a `MonoMod.MonoModRules` type from the patch assembly at patch time. The constructor runs **before** `patch_` members are copied, so it cannot reference newly-added methods. Register a `PostProcessor` delegate on `modder.PostProcessors` to defer the actual IL insertion until **after** all patching and reference-fixing is complete. At that point, every new member from `patch_` classes exists on the target type.

Pipeline order in `MonoModder.AutoPatch`:

1. `ParseRules` (runs `MonoModRules` constructor)
2. `PrePatchModule` (create stub types)
3. `PatchModule` (copy members, generate `orig_`, apply patches)
4. `PatchRefs` (fix all references)
5. `PostProcessors` (your IL insertion runs here)

### dnSpy-Visible Markers

Insert markers so the patched IL is auditable in dnSpy:

- **Insertion-point marker**: emit a `ldstr "tag"; call __PatchMarker(string)` instruction pair before the actual inserted logic. The string tag identifies which insertion this is. Stack effect is neutral (push string, call consumes it).
- **Method-level marker**: add a `[MonoMod.PatchInsertMarker]` custom attribute to the patched method so it shows up in dnSpy's member list.
- Both the marker attribute type and the `__PatchMarker` no-op method are created in the target module via Cecil during the Rules constructor, so they survive into the patched output.

### Minimal Skeleton

```csharp
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.InlineRT;

namespace MonoMod;

public class MonoModRules
{
    static MonoModRules()
    {
        var modder = MonoModRulesManager.Modder;
        var module = modder.Module;

        // 1. Create marker attribute type in target module.
        var voidRef = module.TypeSystem.Void;
        var stringRef = module.TypeSystem.String;
        var attrType = new TypeDefinition("MonoMod", "PatchInsertMarkerAttribute",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            module.ImportReference(typeof(Attribute)));
        var attrCtor = new MethodDefinition(".ctor",
            MethodAttributes.Public | MethodAttributes.SpecialName |
            MethodAttributes.RTSpecialName | MethodAttributes.HideBySig, voidRef);
        var ctorBody = new MethodBody(attrCtor);
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Call,
            module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes))));
        ctorBody.Instructions.Add(Instruction.Create(OpCodes.Ret));
        attrCtor.Body = ctorBody;
        attrType.Methods.Add(attrCtor);
        module.Types.Add(attrType);

        // 2. Create no-op marker method in target module.
        var markerType = new TypeDefinition("MonoMod", "PatchMarkers",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        var markerMethod = new MethodDefinition("__PatchMarker",
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, voidRef);
        markerMethod.Parameters.Add(new ParameterDefinition("tag", ParameterAttributes.None, stringRef));
        var markerBody = new MethodBody(markerMethod);
        markerBody.Instructions.Add(Instruction.Create(OpCodes.Ret));
        markerMethod.Body = markerBody;
        markerType.Methods.Add(markerMethod);
        module.Types.Add(markerType);

        // 3. Defer IL insertion to PostProcessor.
        modder.PostProcessors += PostProcess;
    }

    private static void PostProcess(MonoModder modder)
    {
        var module = modder.Module;
        var markerAttrCtor = module.GetType("MonoMod.PatchInsertMarkerAttribute")
            .Methods.First(m => m.Name == ".ctor");
        var markerMethod = module.GetType("MonoMod.PatchMarkers")
            .Methods.First(m => m.Name == "__PatchMarker");

        // Example: insert B() between A() and C() in Owner.Run().
        InsertMiddleCall(module,
            typeName: "TargetNS.Owner", methodName: "Run",
            anchorCallName: "A", insertCallName: "B",
            tag: "InsertB_AfterA_BeforeC",
            markerMethod: markerMethod, markerAttrCtor: markerAttrCtor);
    }

    /// <summary>
    /// Find the call instruction for anchorCallName, skip past any
    /// pop/stloc that consumes its return value, then insert a marker call
    /// plus the actual inserted method call.
    /// </summary>
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

        // Find anchor: the call instruction matching anchorCallName by method name.
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

        // If the anchor method returns non-void, the compiler emits pop or stloc
        // right after the call. Insert AFTER that instruction to keep the stack balanced.
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

        // Build insertion sequence. InsertAfter works on single instructions,
        // so insert in reverse to preserve order.
        var ldstrTag    = il.Create(OpCodes.Ldstr, tag);
        var callMarker  = il.Create(OpCodes.Call, markerMethod);
        var ldarg0      = il.Create(OpCodes.Ldarg_0);
        var callInsert  = il.Create(OpCodes.Callvirt, insertMethod);

        il.InsertAfter(anchor, callInsert);
        il.InsertAfter(anchor, ldarg0);
        il.InsertAfter(anchor, callMarker);
        il.InsertAfter(anchor, ldstrTag);

        // Method-level marker attribute.
        method.CustomAttributes.Add(new CustomAttribute(markerAttrCtor));
    }
}
```

### Pitfalls (Ordered By Likelihood)

1. **Match by callee, not by position.** Identify the anchor call instruction by `MethodReference.Name` (and ideally `DeclaringType.FullName` + parameter count). Never match by instruction index, because compiler-inserted instructions (box, castclass, static calls) can shift offsets.
2. **Stack balance.** If the anchor method returns non-void and the compiler left a `pop` or `stloc` after the call, insert AFTER that instruction. Inserting between the `call` and `pop`/`stloc` corrupts the stack.
3. **Exception handler regions.** If the anchor is inside a `try` block, inserting instructions between `TryStart` and `TryEnd` is safe because Cecil EH ranges use `Instruction` references that are stable. Do NOT insert across a `leave` boundary.
4. **MonoModRules timing.** The constructor runs before `patch_` members are copied. Use `PostProcessors` for any IL that references newly-added methods. Use the constructor only for creating marker infrastructure and registering the PostProcessor.
5. **Rules type is removed.** After execution, MonoMod removes the `MonoModRules` type from the patch module. Do not put runtime members on it. Marker types must be created in the target module, not in the Rules assembly.
6. **Modder resolution via stack trace.** `MonoModRulesManager.Modder` walks the call stack to find the calling assembly. Call it only from the Rules constructor (or its direct call chain), not from background threads or deferred lambdas.