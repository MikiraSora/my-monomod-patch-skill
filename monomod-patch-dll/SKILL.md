---
name: monomod-patch-dll
description: Build MonoMod.Patcher ahead-of-time .mm.dll patch projects for .NET assemblies. Use when the agent needs to create, edit, or test projects named like Target.PatchName.mm.dll that MonoMod applies to Target.dll/Target.exe to produce a patched assembly, especially when using patch_ classes, MonoModPatch, MonoModIgnore, MonoModConstructor, and orig_ methods.
---

# MonoMod Patch DLL

> This skill is agent-agnostic and works in both Claude Code and Codex.
> - **Claude Code**: `SKILL.md` (this file) is the skill definition; it is discovered by the frontmatter `name`/`description`.
> - **Codex**: register the agent via `agents/openai.yaml` alongside this `SKILL.md`; the `openai.yaml` file is ignored by Claude Code.
> The instructions in this file and in `references/` are identical for both agents.

## Workflow

Use this skill for MonoMod.Patcher `.mm.dll` projects, not runtime `Hook`, `ILHook`, or `MMHOOK_*.dll` mods unless the user explicitly asks to compare them.

### From a git diff (two commits)

If the user provides a **project git repository + two commits** (`base..head`) and wants the source changes between them reproduced as a patch on an already-compiled target assembly, follow `references/git-diff-workflow.md` instead of starting from scratch. That workflow classifies each diff hunk, maps it to a patch pattern, refuses signature changes (hard limit), and pauses on middle-of-method insertions for the user to choose between IL insertion and copy-whole-body. The steps below still apply as the build/apply/verify tail of that workflow.

The classification logic (diff hunk → category) is implemented as an executable, test-backed program at `tests/DiffClassifier/` (library) + `tests/DiffClassifier.Tests/` (50-case fixture). Run `dotnet run --project tests/DiffClassifier.Tests/DiffClassifier.Tests.csproj` to verify the classifier against the 50 cases in `tests/git-diff-cases.md`. The classifier is line-level + brace-depth heuristic (no Roslyn); it gives low confidence on inherently ambiguous boundaries (expression-internal insertion), which the workflow routes to user decision anyway.

1. Inspect the target assembly before writing patch code.
   - Determine the assembly name, target framework, namespace/type names, method signatures, virtual/override status, constructors, and dependencies.
   - Prefer structured inspection with reflection, `dotnet`, ILSpy/dnlib/Mono.Cecil, or existing project sources when available.
2. Create a class library whose output assembly name ends with `.mm`, usually `TargetAssembly.PatchName.mm`.
3. Reference the target assembly and MonoMod.Patcher, but do not copy or redistribute target binaries from the patch project output.
4. Write patch classes using the normal path first:
   - Put `patch_TypeName` in the target type namespace when possible.
   - Use `[MonoModPatch("global::Namespace.TypeName")]` when the patch type cannot follow the `patch_` naming/namespace convention.
   - Inherit from the target type when visible members need to be reused.
   - Declare `extern orig_MethodName(...)` only when the patch needs to call the original implementation.
5. Support `[MonoModConstructor]` for `.ctor` or `.cctor` patches.
6. Use `[MonoModIgnore]` for helper members/types that must compile into the patch assembly but not be copied or patched into the target.
7. If the patch must insert code **between** two existing instructions inside a method body (not just wrap the whole method), the `orig_` wrapper is structurally insufficient. Use `MonoModRules` + `PostProcessor` + Cecil `ILProcessor` instead. Read `references/modifier-recipes.md` section "Precise IL Insertion" before implementing. The rules class must use a **static** constructor (`static MonoModRules()`), not an instance constructor.
8. Build, stage, apply, and verify the patch behavior. Prefer an explicit output path, for example `Target_modded.dll`, instead of relying on `MONOMODDED_Target.dll`.

Read `references/patcher-patterns.md` before implementing a patch project. Read `references/modifier-recipes.md` when the user asks for modifiers beyond `MonoModPatch`, `MonoModIgnore`, and `MonoModConstructor`, OR when the patch requires inserting code between existing instructions inside a method body (see the "Precise IL Insertion" section, verified across 41 scenarios S200-S250).

**BepInEx + MonoMod form:** if the patch project references `BepInEx/core/MonoMod.dll`, outputs to `BepInEx/monomod/`, or targets a game whose patches are applied by BepInEx's MonoMod.Loader at launch (rather than applied offline by you), read `references/bepinex-monomod-environment.md` **before** writing code. That reference covers: detecting this form, asking the user for the `BepInEx/` folder and extracting `MonoMod.dll` + the real target DLL from it, the PE32+ Mono target assembly that MSBuild drops with `MSB3246` (and the Cecil round-trip clean-reference fix), and the runtime `TypeLoadException` on injected types whose dependency DLLs were staged only to `monomod/` (not a runtime probe path). For third-party (non-target) dependencies the patch brings in, the reference says to tell the user which DLLs to stage to `Managed/` / `DumpedAssemblies/` — the skill does not write `PostBuildEvent` copy logic for them.

Do not call `[MonoModIgnore]` helpers from patched method bodies. Ignored helpers are not copied into the target assembly, so patched IL that calls them will fail at runtime unless the call is relinked elsewhere.

## Required Patterns

Basic method wrapper:

```csharp
#pragma warning disable CS0626

namespace TargetNamespace;

internal class patch_TargetType : TargetType
{
    public extern string orig_FormatName(string name);

    public string FormatName(string name)
    {
        return "[patched] " + orig_FormatName(name).ToUpperInvariant();
    }
}
```

Explicit target type:

```csharp
using MonoMod;

[MonoModPatch("global::TargetNamespace.TargetType")]
internal class PatchTargetType : TargetNamespace.TargetType
{
}
```

Constructor patch:

```csharp
#pragma warning disable CS0626
using MonoMod;

namespace TargetNamespace;

internal class patch_TargetType : TargetType
{
    public extern void orig_ctor(string value);

    [MonoModConstructor]
    public void ctor(string value)
    {
        orig_ctor(value);
        Marker = "constructed by patch";
    }
}
```

For the tested current pattern, use a normal method named `ctor` with `[MonoModConstructor]`; its original stub is `orig_ctor`. If this fails against a different MonoMod version or a real C# constructor pattern, inspect MonoMod's constructor handling and the patched IL before guessing a different stub name.

## Project Guidance

Use a patch project shape like this, adapted to the target TFM:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>TargetAssembly.PatchName.mm</AssemblyName>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <!-- 默认输出整洁: 仅产出 patch .mm.dll 自身, 不拷贝 MonoMod*/Mono.Cecil*/System* 等传递依赖 -->
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="TargetAssembly">
      <HintPath>..\path\to\TargetAssembly.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <PackageReference Include="MonoMod.Patcher" Version="25.0.1" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Match the target assembly framework when practical. For Unity/old .NET Framework targets, prefer the target's framework profile over modern `net8.0`.

**Output cleanliness (default):** unless the user says otherwise, the build output must contain only `TargetAssembly.PatchName.mm.dll` (+ `.pdb`) — not the target DLL, not Unity runtime DLLs, not `System.*.dll`, not `MonoMod.*`/`Mono.Cecil.*` tooling DLLs. `CopyLocalLockFileAssemblies=false` plus `<Private>false</Private>` on every target/Unity/System reference achieves this. See `references/patcher-patterns.md` "Patch Project Output Cleanliness" for the full rule and the opt-in carve-out.

**Process artifacts isolation (default):** anything produced *during* building/testing/inspecting/applying that is NOT part of the patch project's own source — helper tool projects (IL inspectors, in-process patcher harnesses), IL dumps, staging copies of the target + game DLLs, the applied `*_modded.dll`, throwaway scripts, downloaded dependencies — must live in a **separate `temp/` (or sibling) directory outside the patch project folder**, never mixed into the patch project folder, and never wired into the patch `.csproj`.

Two failure modes this prevents, both hit in practice:

- **Folder pollution:** copying the whole game `Managed/` (Unity runtime DLLs, the target DLL, dozens of `UnityEngine.*.dll`) into the patch project folder to stage patching dumps binaries you do not own next to your source and obscures what is actually yours. Stage in `temp/` instead.
- **Reference hijack via assembly unification:** placing a *tool* sub-project (e.g. an IL-inspector or patcher harness built against NuGet `Mono.Cecil 0.11.6` / `MonoMod.Utils 25.0.12`) **inside** the patch project tree lets MSBuild assembly unification resolve the patch project's `<Reference HintPath="...\Mono.Cecil.dll">` to the tool's higher-version `bin/` copy instead of the intended HintPath. The `.mm.dll` then silently references the wrong Cecil/MonoMod version and breaks at patch-time. Keep tool projects in a sibling directory (e.g. `..\tools\`), not under the patch project root. If a tool must share the tree, exclude it from the patch build (`<Compile Remove="tools\**\*.cs" />`) AND verify the `.mm.dll` `AssemblyRef` table afterwards — exclusion alone does not stop unification from seeing the tool's `bin/`.

Rule of thumb: after building, the patch project folder should contain only patch source (`.mm.cs`, `MonoModRules.cs`), the `.csproj`, and `bin/`/`obj/` (whose sole non-pdb output is the `.mm.dll`). Everything else goes to `temp/`.

## Verification

A reliable verification loop is:

1. Build the target and patch project.
2. Copy the target assembly, patch `.mm.dll`, and required dependencies to a staging directory **under `temp/`, outside the patch project folder** (see "Process artifacts isolation" above — do not stage game DLLs inside the patch project).
3. Apply MonoMod.Patcher:
   - Default CLI style: `MonoMod.Patcher.dll Target.dll`
   - Explicit output style: `MonoMod.Patcher.dll Target.dll Target.PatchName.mm.dll Target_modded.dll`
   - In-process harness style: instantiate `MonoMod.MonoModder`, call `Read`, `ReadMod`, `MapDependencies`, `AutoPatch`, and `Write`.
4. Assert the patched assembly exists and contains `MonoMod.WasHere`.
5. Execute or reflect over the patched assembly to prove behavior changed.

Keep tests on local toy assemblies unless the user provides a legal target and asks to patch it.
