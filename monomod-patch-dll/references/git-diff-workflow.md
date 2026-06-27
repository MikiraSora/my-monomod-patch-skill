# From a Git Diff: Generate a .mm.dll Patch From Two Commits

Use this workflow when the user gives a **project git repository + two commits** (`base..head`) and wants the source changes between those commits reproduced as a MonoMod `.mm.dll` patch applied to an already-compiled target assembly.

The core tension: **a git diff is source-level; a MonoMod patch is binary/IL-level.** This workflow is the mapper between them. It classifies each hunk, maps it to a patch pattern, and refuses to silently fake anything a patch cannot express.

## Prerequisites And Scope Question

Before classifying, confirm with the user which of two models applies (support both; the user states which at call time):

- **Model A — target ≡ base build**: The target assembly is (or is equivalent to) what the `base` commit compiles to. The patch reproduces the `head` changes on top of it. Signature alignment against the target is straightforward.
- **Model B — released target, diff describes intent**: The target assembly is a released version that may not be a clean build of `base`; the two commits live on a recompilable source copy and the diff only describes *what to change*. Signature alignment must be strict and verified against the actual target metadata, not assumed from `base`.

Both models converge on the same pipeline below; Model B just makes step 3 (Align) non-negotiable rather than a sanity check.

## Workflow

1. **Get the diff.** Restrict to source: `git diff base..head -- '*.cs'` (handle `.csproj`/config changes separately — they rarely map to patches and usually just change build wiring).
2. **Classify hunks.** Group changes by type/member. Each hunk falls into one row of the mapping table below.
3. **Align to the target assembly.** Inspect the target DLL (`dotnet`/reflection/Mono.Cecil/ILSpy) and confirm every type/method the diff touches exists in the target with a matching signature. Under Model B this is the source of truth, not `base`.
4. **Map to a patch pattern** per the table. Signature-changing hunks cannot map — route them to the report.
5. **Generate the patch project.** One `.mm.cs` file **per source file** in the diff (see Organization below), plus the `.mm.csproj`.
6. **Build, stage, apply, verify** as in the base skill workflow.

## Diff Classification → Patch Pattern

| diff type | mappable | patch pattern | notes |
|---|---|---|---|
| change method body (logic) | yes | `orig_` wrapper + new implementation | most common, most stable |
| change method body + middle insertion | decide | IL insertion **or** copy-whole-body — see below | `orig_` cannot express "insert B between A and C" |
| add field/property/method | yes | declare in `patch_` type | member initializers are NOT copied — patch the ctor to assign |
| add new type | yes | new class in patch assembly | goes into target as-is |
| change accessibility (private→public) | yes | `[MonoModPublic]` | simple |
| change `const` value | yes | re-declare `const` in `patch_` | const is metadata, copied |
| change auto-property initializer | partial | convert to ctor-patch assignment | initializer IL is not copied |
| remove member/type | destructive | `[MonoModRemove]` | warn; may break callers |
| **signature change** (add/drop param, change return type, change generics, instance↔static, change virtuality) | **no** | — | hard limit; route to report |
| `.csproj`/config/`#if`-directive change | no | — | not IL; route to report |

## Hard Limit: Signature Changes

A signature change alters the *contract* of an existing member. The patch mechanism can replace a method body, add members, or remove members, but it cannot change an existing member's signature — `patch_`/`orig_` only attach when signatures match.

Examples of hard-limit hunks:

- add/remove a parameter, or change `ref`/`out`/`in`
- change return type (`int` → `long`)
- change generic parameters (`T Foo<T>()` → `T Foo<T,U>()`)
- flip instance ↔ static
- change overrideability in a direction the target does not allow

**Handling: do NOT attempt a fallback.** Skip the hunk, write it to the report, and prompt the user to decide. Do not silently emit a `[MonoModReplace]`+relink guess — it tends to compile but crash at runtime.

## Middle-Of-Method Insertion: Let The User Decide

When a hunk inserts code *between* two existing statements inside a method body, `orig_` (which treats the original body as an indivisible black box, callable only before/after) cannot express it. Two viable approaches — **the user chooses per hunk**, not the skill:

1. **IL insertion** — `MonoModRules` + `PostProcessor` + Cecil `ILProcessor`. Precise, preserves the original body, but requires inspecting the target method IL to identify the anchor call (by callee name + occurrence) and confirm the stack-neutral insertion point. Use the skeleton in `modifier-recipes.md` "Precise IL Insertion". Fragile if the anchor is ambiguous.
2. **Copy whole body** — replace the method entirely with `[MonoModReplace]` (or a no-`orig_` wrapper), hand-writing the original body's logic *plus* the inserted code. No `orig_` call, so no dependency on the original implementation. Simpler and robust, but you re-express the original logic by hand and must keep it in sync if the target body ever changes.

**Presentation (per the agreed approach): list each middle-insertion hunk in the report and STOP.** For each, show both options with their cost/applicability and wait for the user to reply which to use before generating that hunk's patch code. Do not pre-generate either.

## Organization: One Patch File Per Source File

Mirror the diff's source structure:

- Each `.cs` file touched by the diff → one `<SourceFileName>.mm.cs` in the patch project.
- A `patch_` type for each target type touched, placed in that type's namespace, living in the file corresponding to its source file.
- If a single source file touches multiple target types, put all their `patch_` types in that one patch file.
- The `MonoModRules` class (only when IL insertion is chosen for some hunk) lives in its own `MonoModRules.cs`, separate from per-source-file patches.

Project shape, reference wiring, and build/stage/apply/verify follow the base `SKILL.md` patterns.

## Report File

Always emit a report (markdown) even when generation completes cleanly, so the user can audit what was automated and what was not. Suggested name: `patch-diff-report.md`, written next to the generated patch project.

Template:

```markdown
# Patch Diff Report

- repo: <path or url>
- diff: <base>..<head>
- target assembly: <path>
- model: A (target≡base build) | B (released target, diff=intent)

## Generated patches

| source file | target type | change kind | pattern used | patch file |
|---|---|---|---|---|
| Enemy.cs | Game.Enemy | body change | orig_ wrapper | Enemy.mm.cs |
| Player.cs | Game.Player | add method | patch_ new member | Player.mm.cs |

## Skipped — signature change (cannot patch)

| source file | member | change | reason |
|---|---|---|---|
| Enemy.cs | Game.Enemy.TakeDamage | added param `bool ignoreArmor` | signature change is a hard limit |

These hunks were NOT patched. Decide per row: drop the change, restructure it
(e.g. add an overload instead of changing the existing method), or use a non-patch approach.

## Needs decision — middle-of-method insertion (generation paused)

| source file | method | insertion summary | option A: IL insertion | option B: copy whole body |
|---|---|---|---|---|
| Engine.cs | Game.Engine.Update | inserted logging between A() and C() | precise, needs IL anchor inspection | re-express whole body, no orig_ |

Reply with which option (A/B) per row to resume generation.

## Other (not IL-mappable)

| source file | change | reason |
|---|---|---|
| Game.csproj | added PackageReference | build wiring, not a patch |
```

## Ordering When Generating

1. Emit all mappable hunks (body changes, new members, new types, accessibility, const) into their per-source-file patches.
2. Emit the `MonoModRules.cs` skeleton **only if** at least one middle-insertion hunk was chosen as IL insertion.
3. Leave middle-insertion hunks ungenerated until the user replies with their per-row choice, then fill in the chosen pattern.
4. Signature-change and non-IL hunks are never generated — they stay in the report.
