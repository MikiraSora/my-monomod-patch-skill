# MonoMod Patch DLL Skill

> Build ahead-of-time `.mm.dll` patch projects for .NET assemblies with [MonoMod.Patcher](https://github.com/MonoMod/MonoMod).

This is an **agent skill** that teaches an AI coding assistant (Claude Code / Codex) how to create, edit, and test MonoMod.Patcher `.mm.dll` patch projects — projects whose output is named `TargetAssembly.PatchName.mm.dll` and which MonoMod applies to `Target.dll` / `Target.exe` to produce a patched assembly. It also ships an executable test suite that verifies generated patches, plus a workflow that maps a git diff onto a patch.

中文版见 [README.md](README.md)。

## Core Capabilities

- **Standard patch patterns**: `patch_TypeName` naming convention, explicit `[MonoModPatch]` targeting, `orig_` wrapping of the original method, `[MonoModConstructor]` constructor patches, `[MonoModIgnore]` helper members.
- **Precise IL insertion**: when code must be inserted between two existing instructions inside a method body, use `MonoModRules` + `PostProcessor` + Cecil `ILProcessor` (verified across 41 scenarios S200–S250).
- **Patch-from-git-diff**: given a project repo + two commits (`base..head`), classify and map source changes onto patch patterns, refuse signature changes a patch cannot express, and pause on ambiguous boundaries for the user to decide.
- **Output cleanliness & isolation**: the patch project output contains only the `.mm.dll` (+ `.pdb`); process artifacts from building/testing/inspecting are isolated to a `temp/` directory outside the project, preventing folder pollution and MSBuild assembly-unification reference hijacking.
- **Agent-agnostic**: Claude Code discovers it via the `SKILL.md` frontmatter; Codex registers it via `agents/openai.yaml`. The instructions are identical for both.

## Quick Start

The skill itself is not a runnable app — it is a knowledge package for agents. To enable it in your own agent:

1. Claude Code: place the `monomod-patch-dll/` directory on the skill discovery path (or reference this repo directly).
2. Codex: register `monomod-patch-dll/agents/openai.yaml` alongside the `SKILL.md` in the same directory.

A minimal patch project (`TargetLibrary.PatchFunctionA.mm.csproj`) looks like:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>TargetLibrary.PatchFunctionA.mm</AssemblyName>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\TargetLibrary\TargetLibrary.csproj"
                      PrivateAssets="all" OutputItemType="Reference">
      <Private>false</Private>
    </ProjectReference>
    <PackageReference Include="MonoMod.Patcher" Version="25.0.1" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Basic method-wrapper example:

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

## Repository Structure

```
monomod-patch-dll/          # skill definition
├── SKILL.md                # main skill instructions (agent entry point)
├── agents/openai.yaml      # Codex registration file (ignored by Claude Code)
├── references/
│   ├── patcher-patterns.md # patch-project patterns, output cleanliness/isolation rules
│   ├── modifier-recipes.md # modifier recipes + precise IL insertion
│   └── git-diff-workflow.md# patch-from-git-diff workflow
└── scripts/                # reserved: deterministic scaffolding/validation scripts

tests/                      # executable test suite
├── ScenarioTargets/        # ~150+ patch-scenario target types (S01–S112 + S100–S250)
├── ScenarioTargets.Patches.mm/  # the matching .mm.dll patch project
├── TestHarness/            # builds target + patch, applies MonoMod, reflection-verifies each scenario
├── DiffClassifier/         # git-diff→patch classifier (library, line-level + brace-depth heuristic)
├── DiffClassifier.Tests/   # 50-case classifier fixture
├── run-all.ps1             # one shot: build → apply patch → verify all scenarios
└── git-diff-cases.md       # 50 classification test cases

examples/MonoModPatchDllExample/  # end-to-end runnable example
└── TargetLibrary / TargetLibrary.PatchFunctionA.mm / PatchHarness

docs/                       # design doc and loop-verification records
```

## Running Tests

```powershell
# Build all test projects, apply the .mm.dll patch, verify all scenarios
pwsh ./tests/run-all.ps1

# Verify the git-diff classifier alone (50 cases)
dotnet run --project tests/DiffClassifier.Tests/DiffClassifier.Tests.csproj
```

`run-all.ps1` builds the solution, runs the `TestHarness` to apply the patch and reflection-verify each scenario, and exits with a code reflecting overall pass/fail.

## Patch from a Git Diff

When the user provides a project git repo + two commits, follow [`references/git-diff-workflow.md`](monomod-patch-dll/references/git-diff-workflow.md): restrict to source `git diff base..head -- '*.cs'` → classify hunks → align to the target assembly metadata → map to a patch pattern (signature changes are refused) → generate the patch project → build/apply/verify. The classification logic has an executable, test-backed implementation (`tests/DiffClassifier/`).

## License

See the repository LICENSE if present. MonoMod itself is governed by its own license.
