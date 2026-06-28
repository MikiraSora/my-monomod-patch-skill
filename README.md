# MonoMod Patch DLL Skill

> 用 [MonoMod.Patcher](https://github.com/MonoMod/MonoMod) 为 .NET 程序集构建预先编译（AOT）的 `.mm.dll` 补丁项目。

English: [README_EN.md](README_EN.md)

这是一个 **agent skill**，教会 AI 编程助手（Claude Code / Codex）如何创建、编辑、测试 MonoMod.Patcher 的 `.mm.dll` 补丁项目——即输出名为 `TargetAssembly.PatchName.mm.dll`、由 MonoMod 应用到 `Target.dll` / `Target.exe` 生成已打补丁程序集的项目。它还附带一套可执行的测试套件来验证生成的补丁，以及一个把 git diff 映射成补丁的工作流。

## 核心能力

- **标准补丁模式**：`patch_TypeName` 命名约定、`[MonoModPatch]` 显式定位、`orig_` 包装原方法、`[MonoModConstructor]` 构造函数补丁、`[MonoModIgnore]` 辅助成员。
- **精确 IL 插入**：在方法体内部两条已有指令之间插入代码时，用 `MonoModRules` + `PostProcessor` + Cecil `ILProcessor`（已跨 41 个场景 S200–S250 验证）。
- **从 git diff 生成补丁**：给定项目仓库 + 两个提交（`base..head`），把源码改动分类映射成补丁模式，拒绝无法表达的签名变更，在歧义边界暂停交由用户决策。
- **构建产物整洁性与隔离**：补丁项目输出仅含 `.mm.dll`（+ `.pdb`）；构建/测试/检查阶段的临时产物隔离到项目外的 `temp/` 目录，避免文件夹污染与 MSBuild 程序集统一劫持引用版本。
- **跨 agent 可用**：Claude Code 通过 `SKILL.md` frontmatter 发现；Codex 通过 `agents/openai.yaml` 注册。两者的指令内容完全一致。

## 快速开始

该 skill 本身不含可运行应用——它是给 agent 用的知识包。要在你自己的 agent 中启用：

1. Claude Code：把 `monomod-patch-dll/` 目录放入 skill 发现路径（或直接引用本仓库）。
2. Codex：注册 `monomod-patch-dll/agents/openai.yaml`，并配合同目录的 `SKILL.md`。

一个最小补丁项目（`TargetLibrary.PatchFunctionA.mm.csproj`）形如：

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

基本方法包装示例：

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

## 仓库结构

```
monomod-patch-dll/          # skill 定义
├── SKILL.md                # 主 skill 指令（agent 读取入口）
├── agents/openai.yaml      # Codex 注册文件（Claude Code 忽略）
├── references/
│   ├── patcher-patterns.md # 补丁项目模式、产物整洁性/隔离规则
│   ├── modifier-recipes.md # 修饰符配方 + 精确 IL 插入
│   └── git-diff-workflow.md# 从 git diff 生成补丁的工作流
└── scripts/                # 预留：确定性脚手架/校验脚本

tests/                      # 可执行测试套件
├── ScenarioTargets/        # ~150+ 补丁场景目标类型（S01–S112 + S100–S250）
├── ScenarioTargets.Patches.mm/  # 对应的 .mm.dll 补丁项目
├── TestHarness/            # 构建目标 + 补丁，应用 MonoMod，反射验证每个场景
├── DiffClassifier/         # git diff→patch 分类器（库，行级 + 花括号深度启发式）
├── DiffClassifier.Tests/   # 分类器 50 例测试夹具
├── run-all.ps1             # 一键：构建 → 应用补丁 → 验证所有场景
└── git-diff-cases.md       # 50 个分类测试用例

examples/MonoModPatchDllExample/  # 端到端可运行示例
└── TargetLibrary / TargetLibrary.PatchFunctionA.mm / PatchHarness

docs/                       # 设计文档与循环验证记录
```

## 运行测试

```powershell
# 构建全部测试项目、应用 .mm.dll 补丁、验证所有场景
pwsh ./tests/run-all.ps1

# 单独校验 git diff 分类器（50 例）
dotnet run --project tests/DiffClassifier.Tests/DiffClassifier.Tests.csproj
```

`run-all.ps1` 会构建解决方案、运行 `TestHarness` 应用补丁并逐场景反射验证，退出码反映整体通过/失败。

## 从 git diff 生成补丁

当用户提供「项目 git 仓库 + 两个提交」时，遵循 [`references/git-diff-workflow.md`](monomod-patch-dll/references/git-diff-workflow.md)：限制源码 `git diff base..head -- '*.cs'` → 按 hunk 分类 → 对齐目标程序集元数据 → 映射到补丁模式（签名变更直接拒绝）→ 生成补丁项目 → 构建/应用/验证。分类逻辑有可执行、带测试的实现（`tests/DiffClassifier/`）。

## 许可证

本项目采用 [MIT 许可证](LICENSE)（Copyright © 2026 Mikira Sora）。MonoMod 本身遵循其各自许可证。
