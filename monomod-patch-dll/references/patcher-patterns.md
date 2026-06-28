# MonoMod.Patcher Patch DLL Patterns

## Identify The Target

Before editing code, inspect:

- Assembly file name and simple assembly name. Patch output normally uses `[AssemblyName].Something.mm.dll`.
- Target framework and runtime family.
- Namespace, type, nested type names, method signatures, constructors, accessibility, virtual/override state.
- Whether dependencies must be staged for MonoMod's resolver.

Do not rely on C# source availability. If only DLLs exist, inspect metadata with reflection or Mono.Cecil.

## Patch Type Mapping

MonoMod maps patch types by name:

- `namespace Game; class patch_Player : Player { ... }` targets `Game.Player`.
- `[MonoModPatch("global::Game.Player")] class AnyName : Game.Player { ... }` explicitly targets `Game.Player`.

Use `[MonoModPatch]` when:

- The target type name is awkward, nested, generated, or conflicts with C# naming.
- The patch type lives in a different namespace.
- Multiple patch files need clearer compile-time names.

## Method Patching

To wrap an existing method:

```csharp
#pragma warning disable CS0626

internal class patch_Service : Service
{
    public extern int orig_Calculate(int value);

    public int Calculate(int value)
    {
        return orig_Calculate(value) + 10;
    }
}
```

Rules:

- Match the target method signature exactly unless intentionally replacing overloads.
- Preserve `static`, instance, return type, parameter order, `ref`/`out`, generic parameters, and accessibility as closely as possible.
- For overrides, keep `override` when the target method is overrideable in C#.
- Omit the `orig_` method only when the patch should fully replace behavior.

## When orig_ Is Not Enough: Middle-Of-Method Insertion

The `orig_` wrapper treats the original method body as an indivisible black box. You can only add code before or after `orig_MethodName()`. If the requirement is to insert code **between** two existing calls inside the original method (for example, the original calls A then C, and you need A then B then C), `orig_` cannot express this.

In this case, use `MonoModRules` with a `PostProcessor` delegate to perform Cecil-level IL insertion. The approach:

1. Add the insertion method (e.g., `B()`) via a normal `patch_` class so MonoMod copies it into the target type.
2. Add a `MonoMod.MonoModRules` class with a **static** constructor that creates dnSpy-visible marker infrastructure and registers a `PostProcessor`.
3. The `PostProcessor` runs after all patching is complete. It uses `ILProcessor.InsertAfter` or `InsertBefore` to inject a call to `B()` at the precise instruction boundary.
4. Always inspect the target method IL first (with ILSpy, dnSpy, or a Cecil inspector) to identify the anchor call instruction by callee method name and confirm the occurrence count.

See `references/modifier-recipes.md` section "Precise IL Insertion" for the full skeleton, 8 tested insertion variants, verified EH region scenarios, and 10 ordered pitfalls. This approach was verified across 41 scenarios (S200-S250) covering void calls, non-void returns, loops, try/catch/finally, switch branches, generics, ref params, boxed args, string constants, recursive methods, and more.

## Constructor Patching

Use `[MonoModConstructor]` for constructor patches:

```csharp
#pragma warning disable CS0626
using MonoMod;

internal class patch_Widget : Widget
{
    public extern void orig_ctor(string name);

    [MonoModConstructor]
    public void ctor(string name)
    {
        orig_ctor(name);
        Name = "patched:" + Name;
    }
}
```

For the tested current pattern, use a normal method named `ctor` with `[MonoModConstructor]`; its original stub is `orig_ctor`. For static constructors or real C# constructor patch patterns, verify the original stub name against the MonoMod version and patched IL.

## Ignoring Helpers

Use `[MonoModIgnore]` for helper members that should remain only in the patch assembly:

```csharp
using MonoMod;

internal class patch_Service : Service
{
    [MonoModIgnore]
    private static string Prefix => "[patched] ";
}
```

Prefer separate helper types marked `[MonoModIgnore]` when helpers are substantial.

Do not call ignored helpers from code that will be copied into the target. The patcher skips ignored members, so a patched method body that still calls an ignored helper can produce a missing-method failure at runtime.

## Adding New Members

New fields, properties, and methods declared in a `patch_` type are copied into the target type. But member initializers are not.

```csharp
internal class patch_Thing : Thing
{
    public string ExtraField = "extra";     // initializer is NOT applied
    public string ExtraProp { get; set; } = "prop"; // initializer is NOT applied
}
```

C# compiles field and auto-property initializers into the constructor IL. MonoMod copies only the member definitions, not those initializer instructions, so the members exist on the patched type but start at their default values (`null`, `0`, ...).

To initialize added members, patch the constructor and assign them there:

```csharp
#pragma warning disable CS0626
using MonoMod;

internal class patch_Thing : Thing
{
    public string ExtraField;
    public string ExtraProp { get; set; }

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        ExtraField = "extra";
        ExtraProp = "prop";
    }
}
```

`const` fields are an exception: their constant values are copied because they are metadata, not constructor IL.
## Project File Notes

- Set `<AssemblyName>Target.PatchName.mm</AssemblyName>`.
- Reference the target assembly with `<Private>false</Private>`.
- Keep `MonoMod.Patcher` private to the patch project unless the user's packaging requires otherwise.
- Use `AppendTargetFrameworkToOutputPath=false` only when a flat build output simplifies staging; otherwise copy from the TFM output directory explicitly.

## Patch Project Output Cleanliness (default)

Unless the user explicitly says otherwise, the patch project's build output directory must contain **only the patch `.mm.dll` itself** (plus its own `.pdb`). It must NOT include:

- the target assembly being patched (`Target.dll` / `Assembly-CSharp.dll` / ...),
- the game's Unity runtime DLLs (`UnityEngine.*.dll`, `Unity.*.dll`, ...),
- framework / BCL DLLs (`System.*.dll`, `mscorlib.*`, ...),
- the patcher's own tooling DLLs (`MonoMod.*.dll`, `Mono.Cecil.*.dll`, `MonoMod.Patcher.exe`).

**Rationale:** the `.mm.dll` is consumed by MonoMod.Patcher — a tool that brings its own Cecil/MonoMod. At patch-time the patch's code is merged into the target; the `.mm.dll` never runs side-by-side with its own deps inside the game. Dumping target/Unity/System/MonoMod DLLs into the output pollutes it, risks redistributing binaries you do not own, and can confuse the patcher's dependency resolver.

**Default mechanism** — put this in the patch `.csproj`:

```xml
<PropertyGroup>
  <!-- do not copy PackageReference transitive deps (MonoMod.Patcher, Mono.Cecil, System.ValueTuple, ...) -->
  <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
</PropertyGroup>

<!-- every Reference to the target / Unity / System / game-runtime DLL: -->
<Reference Include="...">
  <HintPath>...</HintPath>
  <Private>false</Private>   <!-- CopyLocal=false: do not copy into output -->
</Reference>

<PackageReference Include="MonoMod.Patcher" Version="25.0.1" PrivateAssets="all" />
```

Result: `bin/.../TargetAssembly.PatchName.mm.dll` (+ `.pdb`) — nothing else.

**Carve-out (opt-in only):** only if the user asks, or a patch genuinely bundles a third-party dependency it must ship alongside (rare — patches normally reuse the target's own dependencies, resolved from the staging dir at patch-time), set `<Private>true</Private>` (CopyLocal) on that single reference to include it. The four excluded categories above stay excluded unless the user explicitly overrides.

**In-process test harness exception:** an in-process harness that runs `MonoModder` itself needs Cecil/MonoMod staged — source those from the NuGet package cache or a dedicated tools directory, not from the patch project output. Do not flip `CopyLocalLockFileAssemblies=true` on the distributable patch project just to feed a harness; keep the patch output clean and let the harness stage its own tooling.

**Process artifacts isolation (default):** the harness project, IL-dump tool, staging copies of the target + game DLLs, and the applied `*_modded.dll` are NOT patch source — they must live in a `temp/` (or sibling) directory **outside the patch project folder**, and must never be wired into the patch `.csproj`. A tool sub-project placed *inside* the patch project tree is especially dangerous: MSBuild assembly unification can resolve the patch's `<Reference HintPath=...>` to the tool's higher-version `bin/` copy (e.g. NuGet `Mono.Cecil 0.11.6` over a BepInEx `0.10.4` HintPath), silently making the `.mm.dll` reference the wrong dependency version. Keep tool projects in a sibling directory. After building, the patch project folder should contain only patch source + `.csproj` + `bin/`/`obj/` (whose sole non-pdb output is the `.mm.dll`); everything else goes to `temp/`. See `SKILL.md` "Process artifacts isolation" for the full rule.


## Applying Patches

MonoMod.Patcher CLI behavior:

- `MonoMod.Patcher.dll Target.dll` scans the target directory for `.mm.dll` files and writes `MONOMODDED_Target.dll`.
- `MonoMod.Patcher.dll Target.dll Patch.mm.dll Target_modded.dll` uses explicit patch and output paths.

In-process harness behavior:

```csharp
using var mm = new MonoMod.MonoModder
{
    InputPath = targetPath,
    OutputPath = outputPath
};
mm.Read();
mm.ReadMod(patchPath);
mm.MapDependencies();
mm.AutoPatch();
mm.Write();
```

Add target, patch, and dependency directories to the staging folder (under `temp/`, outside the patch project folder) before patching.
