# MonoMod Patch DLL Skill Design Draft

## Goal

Create a Codex skill that teaches an agent how to build MonoMod.Patcher-based `.mm.dll` patch projects and a companion test project that verifies generated patches against a small target assembly.

This document is iterative. Decisions marked `Open` should be resolved with the user one at a time and then folded back into the skill design.

## Researched Sources

- MonoMod official repository: https://github.com/MonoMod/MonoMod/
  - Local snapshot: `.research/MonoMod`
  - Commit inspected: `d798a2d11d68638abee673493ca8af957933711a` from 2026-06-22
- Official docs inspected:
  - `.research/MonoMod/README.md`
  - `.research/MonoMod/docs/README.Patcher.md`
  - `.research/MonoMod/docs/RuntimeDetour/Usage.md`
  - `.research/MonoMod/docs/RuntimeDetour.HookGen/Usage.md`
  - `.research/MonoMod/docs/README.RuntimeDetour.md`
  - `.research/MonoMod/src/MonoMod.UnitTest/RuntimeDetour/ILHookTest.cs`
- External search topics inspected:
  - MonoMod patch DLL tutorial
  - MonoMod.Patcher `.mm.dll` examples
  - MonoMod RuntimeDetour `Hook` and `ILHook` examples
  - HookGen `MMHOOK_*.dll` tutorials

## Key Technical Facts

- MonoMod has multiple related but distinct workflows:
  - `MonoMod.Patcher`: ahead-of-time assembly rewriting. Patch projects build files named like `[Assembly].Something.mm.dll`. Running `MonoMod.exe Assembly.dll` scans the input directory for matching `.mm.dll` files and emits `MONOMODDED_[Assembly].dll` by default. The CLI also accepts an explicit output path as the final argument, so a harness can generate `Assembly_modded.dll` if desired.
  - `MonoMod.RuntimeDetour`: runtime method detouring with `Hook` and IL rewriting with `ILHook`. Hook lifetime matters; disposed or collected hooks are undone.
  - `MonoMod.RuntimeDetour.HookGen`: generates `MMHOOK_[Assembly].dll` helper assemblies with `On.*` and `IL.*` event APIs. Official docs now recommend direct RuntimeDetour for anything beyond simple use cases.
- For Patcher `.mm.dll` projects:
  - Patch classes are normally named `patch_TypeName` in the same namespace as the target type.
  - Alternatively, use `[MonoModPatch("global::Namespace.TypeName")]` when the patch class cannot live in the same namespace/name pattern.
  - Inheriting from the original type allows patch code to reuse visible members.
  - Declare `extern` `orig_MethodName` members to call the copied original method.
  - The patch assembly should reference the target assembly with copy-local disabled to avoid redistributing target binaries.
  - A `MonoMod.MonoModRules` type can run at patch time to define relinks, flags, custom modifiers, or Cecil-level changes.
  - Use standard MonoMod modifiers such as `[MonoModIgnore]`, `[MonoModConstructor]`, `[MonoModReplace]`, `[MonoModOriginalName]`, `[MonoModLinkTo]`, `[MonoModLinkFrom]`, and `[MonoModIfFlag]` when needed.
  - If the patcher is invoked with only the input assembly path, it scans the input assembly directory for `.mm.dll` files. If invoked with multiple arguments, the middle arguments are explicit mod paths and the last argument is the output path.
- For runtime hook projects:
  - Use `Hook` when replacing or wrapping a method via reflection/delegates.
  - Use `ILHook` plus `MonoMod.Cil.ILCursor` when editing IL instructions.
  - Keep hook objects alive for as long as the patch should stay active.
  - Use `DetourConfig` when multiple mods need predictable hook ordering.

## MonoMod Modifier Attribute Inventory

These attributes were inspected from `.research/MonoMod/src/MonoMod.Patcher/Modifiers` and the corresponding handling in `MonoModder.cs`.

Required first-version attributes:

| Attribute | Main use |
| --- | --- |
| `MonoModPatch` | Put on a type to make it behave like a `patch_` type targeting the supplied full type name. Useful when the patch class cannot be named or namespaced like the target. |
| `MonoModIgnore` | Ignore a type/member for normal patching. Custom MonoMod attribute handlers may still be applied. |
| `MonoModConstructor` | Treat a patch method as a constructor, or allow constructor patching. Required for constructor patch examples and tests. |

Optional recipe attributes:

| Attribute | Main use |
| --- | --- |
| `MonoModReplace` | Replace existing method body without generating an `orig_` copy; for fields it replaces type/attributes, for properties/types it removes the old one before adding the new one. |
| `MonoModRemove` | Remove the target type/member from the patched assembly. |
| `MonoModPublic` | Make a type/member public during post-processing. |
| `MonoModOriginalName` | Override the generated `orig_` method name for a patch method. |
| `MonoModOriginal` | Mark a method as an original-method stub even if it does not use the `orig_` prefix. |

Useful but more situational:

| Attribute | Main use |
| --- | --- |
| `MonoModAdded` | Automatically applied by MonoMod to newly added types/members. Agents normally should not need to write it manually. |
| `MonoModEnumReplace` | For enum patch types, remove existing enum value fields before applying values from the patch. |
| `MonoModNoNew` | On methods, skip the patch if the target method does not already exist. Current field handling is stricter: a field marked with it is skipped. |
| `MonoModTargetModule` | Filter a patch type/member to a specific target module name/full assembly name. Useful when one patch DLL supports multiple target assemblies/versions. |
| `MonoModIfFlag` | Conditionally apply a type/member based on a flag in `MonoModder.SharedData`, usually set from `MonoModRules`. |
| `MonoModOnPlatform` | Intended as platform filtering with `OSKind`; verify behavior in the MonoMod version in use before relying on it. |

Advanced relinking/customization:

| Attribute | Main use |
| --- | --- |
| `MonoModLinkFrom` | Static relink: calls/references to the supplied findable ID are relinked to the annotated type/member. |
| `MonoModLinkTo` | Relink calls/references to the annotated item toward another target. |
| `MonoModHook` | Obsolete alias-style static hook attribute; source marks it obsolete and recommends `MonoModLinkFrom` or RuntimeDetour/HookGen. |
| `MonoModForceCall` | Force calls to an annotated method to use IL `call`. |
| `MonoModForceCallvirt` | Force calls to an annotated method to use IL `callvirt`. |
| `MonoModCustomAttributeAttribute` | Register a custom attribute handler method from `MonoModRules`. |
| `MonoModCustomMethodAttributeAttribute` | Register a method-level custom attribute handler from `MonoModRules`, including optional `ILContext` handling. |
| `MonoMod__SafeToCopy__` | Internal marker used by MonoMod modifier definitions. Do not use in patch projects. |

## Recommended Skill Shape

Skill folder name: `monomod-patch-dll`.

Suggested resources:

- `SKILL.md`: concise workflow and decision tree.
- `references/patcher-patterns.md`: AOT `.mm.dll` project patterns, csproj guidance, modifiers, MonoModRules, and verification.
- `references/modifier-recipes.md`: reference-only recipes for optional MonoMod modifiers.
- `scripts/`: optional project generator/validator if we choose to automate scaffolding.
- `assets/templates/`: optional minimal solution template if we choose to include deterministic scaffolding.

The skill should instruct agents to:

1. Default to AOT `.mm.dll` patching unless the user explicitly asks for runtime detours.
2. Inspect the target assembly API and framework before writing code.
3. Build a patch project targeting a compatible TFM.
4. Keep target assembly references private/copy-local false.
5. Implement patch classes with `patch_` naming or `[MonoModPatch]`.
6. Add `orig_` extern methods only when the original method should be called.
7. Support constructor patching with `[MonoModConstructor]` when the requested patch targets `.ctor` or `.cctor`.
8. Use Cecil/MonoModRules only when attribute/class patching is insufficient.
9. Verify by building, patching a sample target, and executing behavioral tests.

## Companion Test Project Proposal

Create a small local .NET solution under `examples/` or `tests/`:

- `TargetApp`: console app or library with simple methods and a behavioral output.
- `TargetApp.Mod.mm`: patch DLL project that changes at least one method while calling `orig_` and includes one `[MonoModConstructor]` constructor patch.
- `PatchHarness`: a test runner that:
  - Builds target and patch projects.
  - Copies MonoMod.Patcher/runtime dependencies into a staging directory.
  - Runs the patcher.
  - Executes the patched assembly or inspects it with reflection.
  - Asserts expected behavior.
- Optional runtime detour sample:
  - `RuntimeDetourMod.Tests` using `Hook` and `ILHook` directly against local test methods.

## Decisions

### Decision 1: Primary Workflow

Question: Should the skill default to AOT MonoMod.Patcher `.mm.dll` projects, runtime `Hook`/`ILHook` mod projects, or both with Patcher first?

Recommended answer: Default to AOT MonoMod.Patcher `.mm.dll` because the user specifically asked for a MonoMod patch DLL code project; include RuntimeDetour and HookGen as a secondary branch so agents do not confuse `.mm.dll`, `MMHOOK_*.dll`, and runtime mods.

Decision: Default to MonoMod.Patcher `.mm.dll` projects that produce patch assemblies such as `XXX.PatchFunctionA.mm.dll`; MonoMod then applies them to `XXX.dll` and writes a patched output assembly. RuntimeDetour is not the primary target.

Status: Accepted.

### Decision 2: Scope Of Patch Authoring Patterns

Question: Should the first version of the skill teach only class/member/constructor patching with `patch_` classes, `orig_` methods, `[MonoModPatch]`, `[MonoModIgnore]`, and `[MonoModConstructor]`, or also require Cecil-level `MonoModRules` examples from the start?

Recommended answer: Teach `patch_` classes, `[MonoModPatch]`, `orig_` methods, added members, ignored members, and `[MonoModConstructor]` as the required first path; include `MonoModRules` as an advanced branch because it raises complexity and needs Mono.Cecil knowledge.

Decision: First version must support `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor` in generated patch DLL projects. `MonoModRules` remains an advanced branch unless explicitly requested.

Status: Accepted.

### Decision 3: Additional Modifier Coverage

Question: Should first-version generated code actively use only `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor`, while documenting other modifiers as reference-only, or should it also generate `MonoModReplace`, `MonoModRemove`, and `MonoModPublic` examples by default?

Recommended answer: Keep active generation to `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor` in the first version; document `MonoModReplace`, `MonoModRemove`, `MonoModPublic`, and relink modifiers as optional recipes. This keeps the skill focused on reliable patch DLL authoring and avoids teaching destructive patch operations as defaults.

Decision: First-version generated code actively uses only `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor`. Other modifiers are reference-only or optional recipes unless the user explicitly requests them.

Status: Accepted.

### Decision 4: Skill Location

Question: Should the skill be installed into `C:\Users\mikir\.codex\skills\monomod-patch-dll` immediately, or generated in the current workspace first?

Recommended answer: Generate and use it in the current workspace first; release/install globally after the skill and test project are validated.

Decision: Create `monomod-patch-dll` in the current workspace for development and testing. Do not install globally yet.

Status: Accepted.

## Risks And Unknowns

- MonoMod packaging and executable names may differ by version or distribution channel; the skill should tell agents to verify the installed package/tool in the target environment.
- Target frameworks vary widely across Unity/Mono, .NET Framework, and modern .NET; the skill should require target assembly inspection before choosing TFM.
- Patching commercial/game assemblies has redistribution and legal constraints; generated examples should use local toy assemblies.
- HookGen documentation is older in style and official docs warn it can struggle; the skill should treat HookGen as optional, not the default path.
