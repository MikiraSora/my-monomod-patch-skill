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


### Insertion Variants (Tested Across 41 Scenarios S200-S250)

All variants below have been verified against live MonoMod.Patcher 25.0.1 on .NET 8. Each produces a patched assembly that passes behavioral reflection tests and contains dnSpy-visible markers. The helpers share a common pattern: find an anchor call instruction by callee method name, optionally skip the return-value consumer (pop/stloc), then emit a marker pair plus the insertion instruction sequence.

**Variant 1: Insert after a call (basic middle-insert)**

`InsertAfter(anchor)`, reverse-order emission. The anchor is a `call`/`callvirt` whose callee name matches. If the anchor returns non-void, skip past `pop`/`stloc` first. Sequence for an instance method with no args:

```
ldstr "tag"
call   __PatchMarker
ldarg_0
callvirt InsertMethod
```

Verified scenarios: S200 (void calls), S203 (try block), S220 (generic List<T>.Add), S223 (ref param), S228 (chained call), S242 (goto flow), S244 (nullable return), S245 (ref struct), S250 (index access).

**Variant 2: Insert before a call (loop body / catch / finally)**

`InsertBefore(target)`, forward-order emission. Used when the insertion must execute at a specific point inside a loop iteration, catch handler, or finally block. The target is the first instruction of the code region to insert before.

Verified scenarios: S210 (for loop), S214 (catch block), S221 (nested catch), S226 (finally), S229 (lock body), S230 (using body), S234 (do-while), S235 (while), S246 (exception filter), S248 (recursive call).

**Variant 3: Insert with one property argument**

Stack order is critical. For `LogValue(int)` with arg from `get_Counter`:

```
ldarg_0      // this for LogValue
ldarg_0      // this for get_Counter
callvirt     get_Counter  // pushes int
callvirt     LogValue     // consumes this + int
```

The `this` for the inserted method goes first, then the argument-loading sequence. Wrong order causes `InvalidProgramException`. Verified: S211, S233 (enum arg, works identically since enums are ints at IL level).

**Variant 4: Insert with two property arguments**

For `LogDimensions(int w, int h)` with args from `get_Width` and `get_Height`:

```
ldarg_0      // this for LogDimensions
ldarg_0      // this for get_Width
callvirt     get_Width
ldarg_0      // this for get_Height
callvirt     get_Height
callvirt     LogDimensions
```

Each argument needs its own `ldarg_0` + `callvirt getter` pair. Verified: S225.

**Variant 5: Insert with a boxed value-type argument**

For `LogBoxed(object)` with arg from `get_BoxedValue` (returns int):

```
ldarg_0      // this for LogBoxed
ldarg_0      // this for getter
callvirt     get_BoxedValue  // pushes int
box          [valuetype]     // boxes int to object
callvirt     LogBoxed        // consumes this + boxed object
```

The `box` opcode takes the property return type as its operand. Verified: S236.

**Variant 6: Insert with a string constant argument**

For `LogTag(string)` with a literal `"mid"`:

```
ldarg_0      // this for LogTag
ldstr        "mid"
callvirt     LogTag
```

No property getter needed. Verified: S237.

**Variant 7: Insert a static method call**

No `ldarg_0`. Use `OpCodes.Call` not `Callvirt`:

```
ldstr "tag"
call   __PatchMarker
call   StaticMethod
```

Verified: S215, S249.

**Variant 8: Marker-only insertion (stack-neutral)**

When the anchor return value is consumed directly on the stack (e.g., `string.Concat(GetPrefix(), GetSuffix())`), you cannot insert a method call between them. Insert only the marker pair before the anchor call, where the stack is empty:

```
ldstr "tag"
call   __PatchMarker
[existing call to GetPrefix -- its return stays on stack]
```

Do NOT insert between the `call` and its consumer. Verified: S213.

**Multiple insertions in the same method**: Call the insertion helper multiple times with different anchor names. Each call independently finds its anchor and inserts. Order of calls in PostProcess does not matter since each anchors on a different instruction. Verified: S227 (two insertions: after Alpha and after Beta).
### Tested EH Region Scenarios

The following exception-handling structures have been verified safe for IL insertion via PostProcessor:

- **try/finally**: Insert in the finally handler region (S226). Safe because Cecil EH ranges use `Instruction` references.
- **try/catch**: Insert in the catch handler region (S214). Same stability guarantee.
- **Nested try/catch**: Insert in the inner catch (S221) or outer try body between inner try (S247). Use occurrence-based matching to target the correct `get_Log` call.
- **catch with `when` filter**: Insert in the filtered catch handler (S246). The filter expression lives outside the handler body, so insertion inside the handler does not affect it.
- **lock statement**: Insert inside the lock body (S229). The compiler generates `Monitor.Enter`/`Monitor.Exit` with a finally handler; insertion in the try body is safe.
- **using statement**: Insert inside the using body (S230). Same pattern as lock: compiler generates `Dispose` in a finally handler.
- **checked context**: Insert inside a checked block (S241). Checked context is a compile-time concept, not an IL instruction, so insertion has no effect on overflow checking.

In all cases, do NOT insert across a `leave` instruction boundary. `leave` is the EH-aware jump; inserting between `leave` and its target corrupts the EH unwinding.

### Pitfalls (Ordered By Likelihood)

1. **Static constructor is mandatory.** `MonoModRulesManager.ExecuteRules` calls `RuntimeHelpers.RunClassConstructor`, which triggers the **static** constructor (`.cctor`), not the instance constructor. Using `public MonoModRules()` silently does nothing; the PostProcessor never registers and no IL is inserted. Always use `static MonoModRules()`.
2. **Match by callee, not by position.** Identify the anchor call instruction by `MethodReference.Name` (and ideally `DeclaringType.FullName` + parameter count). Never match by instruction index, because compiler-inserted instructions (box, castclass, static calls) can shift offsets. This was verified across 41 scenarios where callee-name matching worked reliably even for generic methods (`List<T>::Add`), ref-param methods, and property getters.
3. **Stack balance for parameterized insertions.** If the anchor method returns non-void and the compiler left a `pop` or `stloc` after the call, insert AFTER that instruction. When inserting a method with arguments, the `this` pointer for the inserted method must be pushed **before** the argument-loading sequence. The correct order for `LogValue(get_Counter())` is: `ldarg_0` (this for LogValue), `ldarg_0` (this for getter), `callvirt getter`, `callvirt LogValue`. Missing the first `ldarg_0` causes `InvalidProgramException` at runtime.
4. **Boxed value-type arguments need explicit `box` opcode.** When passing an `int` property value to a method expecting `object`, insert a `box` instruction between the getter call and the method call. The `box` operand is the property return type (`argGetter.ReturnType`). Without it, the CLR throws a type mismatch.
5. **Occurrence counting requires IL inspection.** When a callee appears multiple times (e.g., `get_Log` in both try and catch blocks), use an occurrence counter. But always inspect the actual IL first to confirm the occurrence number. Guessing caused failures in S210 and S214 where the expected second occurrence was actually the first (and only) one.
6. **Exception handler regions.** Inserting inside `try`, `catch`, `finally`, and `when`-filtered catch blocks is safe because Cecil EH ranges use `Instruction` references that are stable across `InsertBefore`/`InsertAfter`. Do NOT insert across a `leave` boundary. See "Tested EH Region Scenarios" above for the full list of verified-safe structures.
7. **MonoModRules timing.** The constructor runs before `patch_` members are copied. Use `PostProcessors` for any IL that references newly-added methods. Use the constructor only for creating marker infrastructure and registering the PostProcessor. Attempting to find a `patch_`-added method in the constructor will fail because it has not been copied yet.
8. **Rules type is removed.** After execution, MonoMod removes the `MonoModRules` type from the patch module. Do not put runtime members on it. Marker types must be created in the target module, not in the Rules assembly.
9. **Modder resolution via stack trace.** `MonoModRulesManager.Modder` walks the call stack to find the calling assembly. Call it only from the Rules constructor (or its direct call chain), not from background threads or deferred lambdas.
10. **Multiple insertions in one method are independent.** Calling the insertion helper multiple times for different anchors in the same method works correctly. Each call finds its own anchor independently. The order of calls in PostProcess does not matter (verified in S227).