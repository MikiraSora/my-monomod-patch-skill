# BepInEx + MonoMod.dll Environment

Read this reference when the patch project targets a game that loads patches through **BepInEx's MonoMod.Loader** — i.e. the `.mm.dll` is dropped into `BepInEx/monomod/` and BepInEx applies it at preload time — rather than being applied offline by `MonoMod.Patcher.exe`/an in-process `MonoModder`.

This is a *different deployment model* from the base skill workflow. The base workflow assumes you build the `.mm.dll`, then **you** apply it to the target and ship the patched `*_modded.dll`. In the BepInEx model, **BepInEx applies the patch at game launch** and loads the result itself — which changes where dependencies must live, what `MonoMod` you reference at compile time, and how build/run failures surface. Two real failure modes below are specific to this model and will not appear in offline patching.

## Step 1 — Detect The BepInEx + MonoMod Form

Treat the patch project as BepInEx+MonoMod if **any** of these hold (the more that hold, the more certain):

| signal | where to look | meaning |
|---|---|---|
| `<Reference Include="MonoMod">` with `<HintPath>` pointing at `BepInEx/core/MonoMod.dll` | patch `.csproj` | references the MonoMod **bundled with BepInEx**, not a NuGet `MonoMod.Patcher` |
| `<OutputPath>` ends in `BepInEx/monomod/` (or a `PostBuildEvent` copies there) | patch `.csproj` | build output is consumed as a BepInEx mod input |
| a `BepInEx/` folder with `core/`, `monomod/`, `config/BepInEx.cfg`, and optionally `DumpedAssemblies/` | game install dir | the runtime environment |
| other `Assembly-CSharp.*.mm.dll` files already in `BepInEx/monomod/` | `BepInEx/monomod/` | confirms the monomod-loader pipeline is in use |
| target assembly lives in `<Game>_Data/Managed/` | game install dir | Unity Managed folder — the runtime assembly-probe path |

If the project references `MonoMod.Patcher` as a NuGet `PackageReference`, outputs to `bin/`, and is applied by you offline, this is **not** the BepInEx form — use the base skill workflow and `patcher-patterns.md` instead.

**Detection does not authorize you to switch modes autonomously.** The signals above are *corroborating hints*, not proof — a `.csproj` can reference `BepInEx/core/MonoMod.dll` for unrelated reasons, and an old `BepInEx/` folder may be a leftover from a previous setup. Once one or more signals hold, **stop and ask the user to confirm** that this patch project is genuinely a BepInEx + MonoMod deployment (`.mm.dll` consumed by BepInEx's MonoMod.Loader at game launch). Do **not** infer the answer, do **not** proceed on the signals alone, and do **not** silently switch from the base workflow into this one. Only after the user explicitly confirms do you treat the project as BepInEx + MonoMod.

## Step 2 — Confirm The Environment With The User And Ask For The BepInEx Folder

Before writing or editing patch code, do two things in the same turn:

1. **Confirm the environment.** Ask the user whether this is really a BepInEx + MonoMod setup (BepInEx applies the `.mm.dll` at game launch via its MonoMod.Loader), as opposed to offline `MonoMod.Patcher` patching. Do not assume — wait for explicit confirmation.
2. **Ask for the BepInEx path.** Whether they can provide the path to the game's `BepInEx/` folder (or the game install root that contains it). This is required for this form — you cannot correctly wire compile-time references or staging without it, because the patch must match the exact MonoMod version the game loads.

If the user declines or is unsure, do **not** guess or fall back to a NuGet `MonoMod.Patcher` to "make it work" — stay paused and let the user decide.

Concretely, from the BepInEx folder you extract:

- **`BepInEx/core/MonoMod.dll`** → the compile-time `<Reference Include="MonoMod">` for the patch project. Use *this* DLL, not a NuGet `MonoMod.Patcher`, so the `[MonoModPatch]` / `[MonoModIgnore]` / `[MonoModConstructor]` attributes resolve to the same types BepInEx's patcher recognizes at apply time. A version mismatch here is a silent runtime break.
- **`<Game>_Data/Managed/<TargetAssembly>.dll`** (e.g. `Assembly-CSharp.dll`) → the assembly being patched. This is the **runtime truth**: type names, method signatures, and field types must match this DLL's metadata, not any source copy.
- **`<Game>_Data/Managed/UnityEngine.dll`** (and other Unity/engine refs the patch touches) → compile-time references.
- **`BepInEx/config/BepInEx.cfg`** → read `[Preloader]` `DumpAssemblies` and `LoadDumpedAssemblies` (see Step 4). These decide where the patched assembly is loaded from at runtime.
- **`BepInEx/monomod/`** → where the built `.mm.dll` (and any sibling dependency DLLs the patch ships) must be placed.

If the user cannot provide the folder, ask at minimum for: the game's `Assembly-CSharp.dll`, the `MonoMod.dll` from their BepInEx `core/`, and where their other `.mm.dll` mods live. Without `MonoMod.dll` matched to their install, do not guess a NuGet version — flag the gap and proceed only with the user's explicit OK.

## Step 3 — Compile-Time Reference Assembly May Need Round-Tripping

A Unity game's `Assembly-CSharp.dll` (and sometimes `UnityEngine.dll`) is frequently a **PE32+ AMD64 Mono image** — valid .NET metadata that Mono/Cecil read fine, but which the **desktop CLR loader rejects**. This does not affect patching (MonoMod uses Cecil metadata resolution), but it breaks the MSBuild compile-time reference.

**Symptom (compile time):** `warning MSB3246: 解析的文件包含错误图像…未能加载…Assembly-CSharp.dll…试图加载格式不正确的程序` in `ResolveAssemblyReferences`, followed by a flood of `error CS0246: 未能找到类型或命名空间名 '<Type>'` for *every* type that lives in the target assembly — and the `csc.exe` command line in the build log is **missing** `/reference:...Assembly-CSharp.dll`. MSBuild's `ResolveAssemblyReference` tried to load the assembly identity via the CLR loader, got `BadImageFormatException (0x80131018)`, and **dropped the reference entirely**. This fails identically on 32-bit and 64-bit MSBuild — it is an image-format rejection, not a bitness issue.

**Confirm the DLL is actually fine** before "fixing" it — the failure is in the *loader*, not the file:

```csharp
// Use System.Reflection.Metadata.PEReader — pure metadata read, no CLR load.
// If this enumerates types, the DLL is valid; only the desktop loader rejects it.
using var pe = new PEReader(File.OpenRead(targetDll));
var md = pe.GetMetadataReader();
foreach (var h in md.TypeDefinitions) { var td = md.GetTypeDefinition(h); /* ... */ }
```

If `PEReader` reads it but `Assembly.LoadFile` / PowerShell `[Reflection.Assembly]::LoadFile` throws `0x80131018`, you have this problem.

**Fix — generate a clean reference assembly via Mono.Cecil round-trip.** Do **not** modify the game's real DLL (MonoMod patches the real one at runtime). Instead, rewrite a *copy* into a desktop-CLR-loadable PE32 MSIL image and reference that copy at compile time only:

```csharp
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(managedDir); // so Cecil can resolve UnityEngine etc. while writing
var ad = AssemblyDefinition.ReadAssembly(realTargetDll, new ReaderParameters {
    InMemory = true, AssemblyResolver = resolver
});
var m = ad.MainModule;
m.Architecture = TargetArchitecture.I386;          // force PE32 MSIL
m.Attributes |= ModuleAttributes.ILOnly;
m.Attributes &= ~ModuleAttributes.Required32Bit;
m.Attributes &= ~ModuleAttributes.Preferred32Bit;
m.Attributes &= ~ModuleAttributes.StrongNameSigned;
ad.Name.PublicKey = null; ad.Name.HasPublicKey = false;
ad.Write(cleanRefDll, new WriterParameters { });
```

Then point the patch `.csproj` at the clean copy:

```xml
<Reference Include="Assembly-CSharp">
  <!-- Compile-time only: the game's real Assembly-CSharp.dll is a PE32+ Mono image
       that the desktop CLR loader (MSBuild RAR) cannot load, so it drops the
       reference (MSB3246) and every type from it becomes CS0246.
       This clean PE32 copy is generated by Cecil round-trip and used ONLY for
       compile-time type resolution. MonoMod still patches the real game DLL. -->
  <HintPath>refs\Assembly-CSharp.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Keep the generated `refs/Assembly-CSharp.dll` inside the patch project folder (it is a build-time artifact you own, not a redistributed game binary in the output). **Regenerate it whenever the game updates `Assembly-CSharp.dll`** — if signatures change, the stale ref will mismatch and break the build or silently mis-resolve.

> Note: this is the *compile-time* mirror of the base skill's "inspect target metadata" step. The base skill inspects with PEReader/Cecil to *read*; here you additionally need a CLR-loadable copy so MSBuild can *reference*. See `patcher-patterns.md` "Identify The Target".

## Step 4 — Runtime Dependency Deployment (The BepInEx-Specific Killer)

In the BepInEx model, MonoMod injects your patch's new types (non-`patch_` classes, new fields' types, etc.) **into the target assembly** and adds `AssemblyRef`s to the patch's own dependencies. At runtime the game loads the patched target and must resolve those refs — but `BepInEx/monomod/` is a **patch-input directory, not a runtime probe path**. Dependencies that live only in `monomod/` are invisible at runtime.

Read `BepInEx/config/BepInEx.cfg` `[Preloader]` to learn where the patched assembly loads from:

| setting | effect |
|---|---|
| `DumpAssemblies = true` | BepInEx writes the patched assembly to `BepInEx/DumpedAssemblies/<target>/` |
| `LoadDumpedAssemblies = true` | the game loads the patched assembly **from `DumpedAssemblies/`** instead of memory (enables dnSpy debugging) |

When `LoadDumpedAssemblies = true`, the patched `Assembly-CSharp.dll` in `DumpedAssemblies/<target>/` is the runtime truth, and its `AssemblyRef`s (to your patch's deps) must resolve from a runtime probe path. The runtime probe paths are, in practice:

1. `<Game>_Data/Managed/` — Unity's standard assembly-probe folder (always works).
2. `BepInEx/DumpedAssemblies/<target>/` — alongside the patched assembly (works when `LoadDumpedAssemblies = true`).

**Symptom (runtime):** `TypeLoadException: Could not load type '<YourPatchNamespace>.<NewType>' from assembly 'Assembly-CSharp…'` at the moment patched code first touches a new type you injected — even though MonoMod's apply log (`[Main] Done.`) reported no error and the patched assembly *contains* the type. The type itself is present; the failure is resolving one of *its* field/parameter types, which lives in a dependency DLL that is not on a probe path.

**Real example (names anonymized).** A patch added a new manager class to `Assembly-CSharp`. That class has fields whose types are defined in a patch-private library `RpcTransport.dll` (`IRpcClient`, `RpcServerListener`, …), and `RpcTransport.dll` itself depends on `WebSocketLib.dll`. The patched `Assembly-CSharp.dll` (in `DumpedAssemblies/<target>/`) correctly contained the new class and correctly `AssemblyRef`'d `RpcTransport` + `PatchContracts`. But only `PatchContracts.dll` had been staged to `DumpedAssemblies/<target>/` (via a `PostBuildEvent`); `RpcTransport.dll` and `WebSocketLib.dll` existed only in `monomod/`. At runtime, JIT-loading the new class tried to resolve `RpcTransport` → not on any probe path → `TypeLoadException`.

**Fix — stage the full transitive dependency closure to a runtime probe path.** For each new type your patch injects, collect every DLL that type's *members* reference, transitively, and copy all of them to `Managed/` and/or `DumpedAssemblies/<target>/`:

```
patch new type  ─refs→  RpcTransport.dll  ─refs→  WebSocketLib.dll
                        PatchContracts.dll ─refs→  RpcTransport.dll
```

All four (the patched target's new-type deps) must be on a probe path. `monomod/` does **not** count.

> This is the BepInEx-model counterpart of the base skill's staging step — but the base skill stages deps for *patch-time* (MonoMod's resolver, in `temp/`), while here you stage deps for *runtime* (the game's CLR, in `Managed/` or `DumpedAssemblies/`). They are different audiences and different folders.

**Third-party (non-target) dependencies are the user's responsibility to stage.** If the patch project references a third-party project/DLL that is *not* part of the target assembly's own dependency graph (i.e. code the patch brings in itself — a transport library, a contracts assembly, a util lib, etc.), do **not** assume MonoMod or the build will place it on a runtime probe path. Tell the user which third-party DLLs the patch needs at runtime and let the user copy them to the right place (`<Game>_Data/Managed/` and/or `BepInEx/DumpedAssemblies/<target>/`, per `BepInEx.cfg`). The skill does not write `PostBuildEvent` copy logic for these — the user decides where their third-party DLLs live.

## Build & Verify Checklist (BepInEx + MonoMod Form)

1. **Reference `BepInEx/core/MonoMod.dll`** (not NuGet `MonoMod.Patcher`) so attribute types match the runtime patcher.
2. **Reference the real target** from `<Game>_Data/Managed/`. If MSBuild emits `MSB3246` and drops it (Step 3), generate a Cecil round-trip clean ref into `refs/` and point `HintPath` there.
3. **`<Private>false</Private>`** on every target/Unity/System reference — keep the patch output (`.mm.dll` + `.pdb`) clean per `patcher-patterns.md` "Patch Project Output Cleanliness".
4. **Output to `BepInEx/monomod/`** (via `OutputPath` or `PostBuildEvent`).
5. **Read `BepInEx.cfg`** `[Preloader]` to know whether runtime loads from `DumpedAssemblies/`.
6. **Tell the user to stage third-party (non-target) dependencies.** Any DLL the patch brings in that is not part of the target's own dependency graph must be copied by the user to a runtime probe path (`Managed/` and/or `DumpedAssemblies/<target>/`); `monomod/` is input-only and not runtime-resolvable.
7. **Run the game** and read `BepInEx/LogOutput.log`:
   - MonoMod apply log should reach `[Main] Done.` with no error — if apply itself fails, that is a *patch-time* problem (signature mismatch, bad `orig_`, ignored-helper call), not this environment's problem.
   - A `TypeLoadException` on one of your injected types *after* `[Main] Done.` is the Step 4 runtime-deployment failure — a transitive/third-party dep is missing from the probe path; tell the user which DLL to stage.
   - Success signal: your patch's own log lines (e.g. a "server started" / "initialized" message from your injected code) appear in `LogOutput.log`.
