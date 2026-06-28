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

## Step 4 — Runtime Dependency Resolution (The BepInEx-Specific Killer)

In the BepInEx model, MonoMod injects your patch's new types (non-`patch_` classes, new fields' types, etc.) **into the target assembly** and adds `AssemblyRef`s to the patch's own dependencies. At runtime the game loads the patched target and must resolve those refs — but `BepInEx/monomod/` is a **patch-input directory, not a runtime probe path**. Dependencies that live only in `monomod/` are invisible at runtime.

Read `BepInEx/config/BepInEx.cfg` `[Preloader]` to learn where the patched assembly loads from:

| setting | effect |
|---|---|
| `DumpAssemblies = true` | BepInEx writes the patched assembly to `BepInEx/DumpedAssemblies/<target>/` |
| `LoadDumpedAssemblies = true` | the game loads the patched assembly **from `DumpedAssemblies/`** instead of memory (enables dnSpy debugging) |

When `LoadDumpedAssemblies = true`, the patched `Assembly-CSharp.dll` in `DumpedAssemblies/<target>/` is the runtime truth, and its `AssemblyRef`s (to your patch's deps) must resolve from a runtime probe path. The default runtime probe paths are:

1. `<Game>_Data/Managed/` — Unity's standard assembly-probe folder (always works).
2. `BepInEx/DumpedAssemblies/<target>/` — alongside the patched assembly (works when `LoadDumpedAssemblies = true`).

`BepInEx/monomod/` is **not** a probe path — it is the patcher's input directory only.

**Symptom (runtime):** `TypeLoadException: Could not load type '<YourPatchNamespace>.<NewType>' from assembly 'Assembly-CSharp…'` at the moment patched code first touches a new type you injected — even though MonoMod's apply log (`[Main] Done.`) reported no error and the patched assembly *contains* the type. The type itself is present; the failure is resolving one of *its* field/parameter types, which lives in a dependency DLL that is not on a probe path.

### Recommended fix — a runtime `AssemblyResolve` resolver (single-folder distribution)

> **This resolver is an *optional* recommended technique, not a default.** Injecting an `AppDomain.AssemblyResolve` hook and an extra patch of a game method is a non-trivial, opinionated choice that changes how the mod distributes its dependencies. **Do not apply it autonomously.** When the Step 4 symptom appears (or proactively, when the patch injects new types referencing third-party deps), **propose** the resolver to the user as one option alongside the manual-staging alternative (below), explain the trade-off (single-folder `monomod/` release vs. extra injected code + a `RuntimeInitializeOnLoadMethod`-style mount), and **wait for the user to explicitly choose**. Only after the user opts in do you implement the resolver. If the user prefers the simpler manual-staging route, follow "Alternative fix" below instead.

The goal: ship the `.mm.dll` **and its third-party dependency DLLs together in one folder** (`BepInEx/monomod/`), with **nothing** needing to be copied to `Managed/` or `DumpedAssemblies/` at release time. Achieve this by injecting a small resolver into the patched assembly that hooks `AppDomain.CurrentDomain.AssemblyResolve` and serves the patch's own dependencies from `monomod/` (or wherever the resolver resolves its base folder to).

**Why `monomod/`-as-distribution-folder is preferable:** `DumpedAssemblies/` is a BepInEx-generated working directory, not a stable release target — a release package should not depend on files being placed there. `monomod/` is the one folder the user *does* control and *does* populate with the mod, so making it the single dependency source yields a clean one-folder release.

**The resolver must be registered early enough — before the first JIT reference to any dependency.** Two mount-point options, in order of reliability:

| mount point | reliable? | notes |
|---|---|---|
| **Explicit patch of the earliest method that first references an injected type** (e.g. patch the game's app-init `Awake`/`initialize*` and call `Register()` before `orig_`) | ✅ **reliable** | `Register()` runs as the first statement of a method that is *itself* the start of the dependency-referencing call chain, so it strictly precedes the first dependency JIT. This is the recommended mount. |
| `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` | ❌ **not reliable in this form** | See trap below. |

**Trap — `[RuntimeInitializeOnLoadMethod]` does NOT fire on BepInEx-patched assemblies.** Unity scans `RuntimeInitializeOnLoadMethod` attributes over assemblies loaded at *engine* init. BepInEx's Preloader patches and **substitutes** the loaded `Assembly-CSharp` after that scan window; a `RuntimeInitializeOnLoadMethod` method newly injected by the patch is **not** registered and **never runs**. Observed in practice: zero trace of the resolver in `LogOutput.log`, and the `TypeLoadException` fires straight from the game's `Awake` chain. Do **not** use `[RuntimeInitializeOnLoadMethod]` as the sole mount for a BepInEx+MonoMod resolver. (It is also only `BeforeSceneLoad`/`AfterSceneLoad` on Unity 5.x — the earlier `SubsystemRegistration`/`BeforeSplashScreen` values are 2019.3+.)

**Resolver design (generalized):**

- An `internal static` class (a plain new type — **not** `patch_`/`[MonoModPatch]` — so MonoMod copies it verbatim into the target). It hooks `AppDomain.CurrentDomain.AssemblyResolve`.
- **Whitelist**: the handler resolves *only* the patch's own dependency simple-names; for everything else it returns `null` and defers to the default resolver. This avoids hijacking other mods' assembly resolution and avoids indiscriminately `LoadFrom`-ing arbitrary DLLs in the search folder.
- **Search directories** (tried in order): ① the resolver's *own* assembly directory (`typeof(Resolver).Assembly.Location` — "based on the patch dll's own folder"); ② `BepInEx/monomod/`, derived via `BepInEx.Paths.BepInExRootPath` (reflect, to avoid a hard compile-time reference to `BepInEx.dll`) with a fallback to `Application.dataPath`-based inference. (Rationale: with `LoadDumpedAssemblies=true`, the patched assembly loads from `DumpedAssemblies/<target>/`, so the resolver's *own* location is **not** `monomod/` — candidate ② is what actually hits the shipped deps.)
- **Logging via `UnityEngine.Debug.Log`** (BepInEx `UnityLogListening=true` routes it to `LogOutput.log`): log the search dirs at `Register()`, and log every hit / load-failure / not-found per dependency. The log call itself must not throw (wrap in try/catch) — a logging failure must not break the resolution chain.
- **Idempotent `Register()`** (guarded by a bool), called by the patch's earliest-dependency-reference method **before** its `orig_`.

Skeleton (generalized; net35-compatible — note `Path.Combine` is 2-arg only on net35):

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace YourPatch.Bootstrap
{
    internal static class DependencyAssemblyResolver
    {
        // Only resolve the patch's OWN dependencies (simple names, case-insensitive).
        private static readonly HashSet<string> OwnDependencies =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // "YourTransportLib", "YourContractsLib", "ThirdPartyLib", ...
            };

        private static string[] _searchDirs;
        private static bool _registered;

        // Called by the patch's earliest method that first references an injected type,
        // BEFORE its orig_. Idempotent.
        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            _searchDirs = ResolveSearchDirs();
            Log("Register: handler attached. SearchDirs=[" +
                (_searchDirs == null ? "<null>" : string.Join(", ", _searchDirs)) + "]");
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromLocal;
        }

        private static Assembly ResolveFromLocal(object sender, ResolveEventArgs e)
        {
            var name = new AssemblyName(e.Name).Name;
            if (!OwnDependencies.Contains(name)) return null;   // defer to default resolver

            var dirs = _searchDirs;
            if (dirs == null) return null;

            foreach (var dir in dirs)
            {
                var path = Path.Combine(dir, name + ".dll");
                if (File.Exists(path))
                {
                    try { var a = Assembly.LoadFrom(path); Log("loaded '" + name + "' from " + path); return a; }
                    catch (Exception ex) { Log("failed '" + name + "' from " + path + " : " + ex.Message); }
                }
            }
            Log("NOT FOUND '" + name + "' in any search dir");
            return null;
        }

        private static string[] ResolveSearchDirs()
        {
            var dirs = new List<string>(2);
            // ① resolver's own assembly directory ("based on the patch dll's own folder").
            var selfDir = GetOwnAssemblyDir();
            if (selfDir != null) dirs.Add(selfDir);
            // ② BepInEx/monomod/ via BepInEx.Paths.BepInExRootPath (reflect), fallback dataPath.
            var monoDir = GetMonomodDir();
            if (monoDir != null && !dirs.Contains(monoDir)) dirs.Add(monoDir);
            return dirs.ToArray();
        }

        private static string GetOwnAssemblyDir()
        {
            try
            {
                var loc = typeof(DependencyAssemblyResolver).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    var d = Path.GetDirectoryName(loc);
                    if (Directory.Exists(d)) return d;
                }
            }
            catch { }
            return null;
        }

        private static string GetMonomodDir()
        {
            try
            {
                var t = Type.GetType("BepInEx.Paths, BepInEx");
                var p = t != null ? t.GetProperty("BepInExRootPath", BindingFlags.Public | BindingFlags.Static) : null;
                var root = p != null ? p.GetValue(null, null) as string : null;
                if (!string.IsNullOrEmpty(root))
                {
                    var d = Path.Combine(root, "monomod");   // net35: 2-arg Combine only
                    if (Directory.Exists(d)) return d;
                }
            }
            catch { }
            try
            {
                // dataPath = <gameRoot>/<Game>_Data; monomod/ = <gameRoot>/BepInEx/monomod
                return Path.GetFullPath(Path.Combine(
                    Path.Combine(Path.Combine(Application.dataPath, ".."), "BepInEx"), "monomod"));
            }
            catch { return null; }
        }

        private static void Log(string msg)
        {
            try { Debug.Log("[DepResolver] " + msg); } catch { }
        }
    }
}
```

**The mount patch** (explicit, reliable) — patch the earliest method whose execution first references one of the injected types, and call `Register()` before `orig_`. Identify that method from the runtime stack trace of the `TypeLoadException` (its top frame):

```csharp
using YourPatch.Bootstrap;
using MonoMod;

namespace YourPatch
{
    [MonoModPatch("global::Game.App.ApplicationRoot")]   // the TypeLoadException's top-frame type
    internal class ApplicationRootEx : global::Game.App.ApplicationRoot
    {
        private extern void orig_initializeFirst();      // the TypeLoadException's top-frame method
        private void initializeFirst()
        {
            DependencyAssemblyResolver.Register();       // MUST precede orig_
            orig_initializeFirst();
        }
    }
}
```

**Timing guarantee:** the patched `initializeFirst` body is `Register(); orig_initializeFirst();`. The patch class and `Register()` reference only already-loaded types (mscorlib/System/UnityEngine), so JIT-ing this patched method does **not** itself need the dependencies. `Register()` attaches the handler; only then does `orig_initializeFirst()` run and first touch an injected type → dependency JIT → `AssemblyResolve` fires → resolver serves it from `monomod/`. ✓

**Why you cannot recover the *original* `.mm.dll`'s file path at runtime:** the `.mm.dll` is merged into the patched `Assembly-CSharp` at patch time and no longer exists as a standalone assembly at runtime; the BepInEx-bundled MonoMod does not retain/ expose the mod source path. What the resolver *can* get is (a) the patched assembly's own load directory (candidate ①) and (b) `BepInEx.Paths`-derived `monomod/` (candidate ②). Candidate ② is what makes single-folder (`monomod/`) distribution work.

> This is the BepInEx-model counterpart of the base skill's staging step — but the base skill stages deps for *patch-time* (MonoMod's resolver, in `temp/`), while here you resolve deps for *runtime* (the game's CLR, via an injected `AssemblyResolve` hook reading from `monomod/`). Different audiences, different mechanisms.

### Alternative fix — stage dependencies to a probe path (no resolver)

If you prefer not to inject a resolver, the fallback is to copy the patch's transitive dependency closure to a default probe path. For each new type your patch injects, collect every DLL that type's *members* reference, transitively, and place all of them on a probe path (`Managed/` and/or `DumpedAssemblies/<target>/`):

```
patch new type  ─refs→  YourTransportLib.dll  ─refs→  ThirdPartyLib.dll
                        YourContractsLib.dll   ─refs→  YourTransportLib.dll
```

All of them must be on a probe path; `monomod/` does **not** count. **Downside:** this couples the release to `DumpedAssemblies/` (a working dir) or pollutes `Managed/` with mod files — less clean than the resolver approach for distribution. **Third-party (non-target) dependencies are the user's responsibility to stage** either way: tell the user which DLLs the patch needs and let the user place them; the skill does not write `PostBuildEvent` copy logic for them.

## Build & Verify Checklist (BepInEx + MonoMod Form)

1. **Reference `BepInEx/core/MonoMod.dll`** (not NuGet `MonoMod.Patcher`) so attribute types match the runtime patcher.
2. **Reference the real target** from `<Game>_Data/Managed/`. If MSBuild emits `MSB3246` and drops it (Step 3), generate a Cecil round-trip clean ref into `refs/` and point `HintPath` there.
3. **`<Private>false</Private>`** on every target/Unity/System reference — keep the patch output (`.mm.dll` + `.pdb`) clean per `patcher-patterns.md` "Patch Project Output Cleanliness".
4. **Output to `BepInEx/monomod/`** (via `OutputPath` or `PostBuildEvent`), and ship the patch's third-party dependency DLLs **alongside** the `.mm.dll` in that same folder.
5. **Read `BepInEx.cfg`** `[Preloader]` to know whether runtime loads from `DumpedAssemblies/`.
6. **If the patch injects new types whose members reference third-party dependencies** (the Step 4 scenario), the `AssemblyResolve` resolver (Step 4 "Recommended fix") is an **optional** technique. **Propose it to the user** alongside the manual-staging alternative and **wait for explicit opt-in** before implementing — do not inject it autonomously. If the user opts in: mount via an **explicit patch of the `TypeLoadException`'s top-frame method** calling `Register()` before `orig_` — **not** `[RuntimeInitializeOnLoadMethod]` (that does not fire on BepInEx-patched assemblies). Whitelist only the patch's own deps; log via `Debug.Log`. If the user declines, use the "Alternative fix" (manual probe-path staging) instead.
7. **Run the game** and read `BepInEx/LogOutput.log`:
   - MonoMod apply log should reach `[Main] Done.` with no error — if apply itself fails, that is a *patch-time* problem (signature mismatch, bad `orig_`, ignored-helper call), not this environment's problem.
   - A `TypeLoadException` on one of your injected types *after* `[Main] Done.` is the Step 4 runtime-resolution failure. Check the resolver's `[DepResolver]` log lines:
     - **No `[DepResolver]` lines at all** → `Register()` never ran → the mount patch didn't apply, or BepInEx loaded a stale patched DLL (delete `DumpedAssemblies/<target>/Assembly-CSharp.dll` + `UnityEngine.dll` to force a re-patch).
     - **`Register` line present but `SearchDirs=[...]` wrong/empty** → search-dir derivation failed; inspect the printed dirs and fix candidate ② (`BepInEx.Paths` / `dataPath`).
     - **`NOT FOUND '<dep>'`** → the dependency DLL is not in any search dir; verify it ships in `monomod/`.
     - **`loaded '<dep>' from ...`** for each dep → resolver working.
   - Success signal: your patch's own log lines (e.g. a "server started" / "initialized" message from your injected code) appear in `LogOutput.log`.
