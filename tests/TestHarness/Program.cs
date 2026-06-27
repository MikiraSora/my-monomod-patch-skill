using System.Reflection;
using System.Text;
using MonoMod;

namespace MonoModTestHarness;

internal static class Program
{
    private static string s_testsRoot = null!;
    private static Assembly s_modded = null!;

    private record Scenario(string Id, string Name, string Requirement, string Expected,
        string TargetFile, string PatchFile, Func<Assembly, ScenarioResult> Check);

    private record ScenarioResult(string Actual, bool Passed);

    private static int Main(string[] args)
    {
        s_testsRoot = FindTestsRoot(AppContext.BaseDirectory);

        var targetDll = Path.Combine(s_testsRoot, "ScenarioTargets", "bin", "Debug", "net8.0", "MonoModTestTargets.dll");
        var patchDll = Path.Combine(s_testsRoot, "ScenarioTargets.Patches.mm", "bin", "Debug", "MonoModTestTargets.Patches.mm.dll");
        var stageDir = Path.Combine(s_testsRoot, "artifacts", "patch-stage");
        var helperDll = Path.Combine(s_testsRoot, "HelperLib", "bin", "Debug", "net8.0", "MonoModHelperLib.dll");
        // Pre-load the helper dependency and resolve subsequent requests from staging dir.
        AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
        {
            var name = new System.Reflection.AssemblyName(e.Name).Name;
            var candidates = new[]
            {
                Path.Combine(s_testsRoot, "artifacts", "patch-stage", name + ".dll"),
                Path.Combine(s_testsRoot, "HelperLib", "bin", "Debug", "net8.0", name + ".dll"),
            };
            foreach (var p in candidates)
                if (File.Exists(p))
                    return System.Reflection.Assembly.LoadFrom(p);
            return null;
        };
        var moddedDll = PatchApplier.Apply(targetDll, patchDll, stageDir, "MonoModTestTargets_modded.dll",
            new Dictionary<string, object?> { ["s98_on"] = true },
            extraDeps: new[] { helperDll });

        s_modded = Assembly.LoadFile(moddedDll);
        Require(s_modded.GetType("MonoMod.WasHere") is not null, "MonoMod.WasHere missing");

        var scenarios = BuildScenarios();
        var sb = new StringBuilder();
        sb.AppendLine("# MonoMod Patch DLL Skill 验证记录");
        sb.AppendLine();
        sb.AppendLine("- 目标程序集: MonoModTestTargets.dll");
        sb.AppendLine("- 补丁程序集: MonoModTestTargets.Patches.mm.dll");
        sb.AppendLine("- 打补丁后: MonoModTestTargets_modded.dll");
        sb.AppendLine("- MonoMod.Patcher: 25.0.1");
        sb.AppendLine("- 说明: 一个目标程序集包含多个场景类, 一个 .mm.dll 补丁程序集包含对应多个 patch_ 类型, 一次性打补丁后逐场景反射验证 (MonoMod 多类型同程序集补丁的标准用法)");
        sb.AppendLine();

        int pass = 0, fail = 0;
        foreach (var sc in scenarios)
        {
            ScenarioResult r;
            try { r = sc.Check(s_modded); }
            catch (Exception ex) { r = new ScenarioResult("EXCEPTION: " + ex.GetType().Name + ": " + ex.Message, false); }

            sb.AppendLine($"## {sc.Id} {sc.Name}");
            sb.AppendLine();
            sb.AppendLine($"**需求**: {sc.Requirement}");
            sb.AppendLine();
            sb.AppendLine($"**期望**: {sc.Expected}");
            sb.AppendLine();
            sb.AppendLine($"**实际**: {r.Actual}");
            sb.AppendLine();
            sb.AppendLine($"**结果**: {(r.Passed ? "PASS" : "FAIL")}");
            sb.AppendLine();
            sb.AppendLine("### 原始目标代码");
            sb.AppendLine("```csharp");
            sb.AppendLine(ReadFile(sc.TargetFile).TrimEnd());
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Patch 代码");
            sb.AppendLine("```csharp");
            sb.AppendLine(ReadFile(sc.PatchFile).TrimEnd());
            sb.AppendLine("```");
            sb.AppendLine();

            if (r.Passed) pass++; else fail++;
            Console.WriteLine($"[{(r.Passed ? "PASS" : "FAIL")}] {sc.Id} {sc.Name} :: expected={sc.Expected} actual={r.Actual}");
        }

        sb.AppendLine("## 汇总");
        sb.AppendLine();
        sb.AppendLine($"- 通过: {pass}");
        sb.AppendLine($"- 失败: {fail}");
        sb.AppendLine($"- 总计: {scenarios.Count}");

        File.WriteAllText(Path.Combine(s_testsRoot, "verify.doc"), sb.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"verify.doc written");
        Console.WriteLine($"PASS={pass} FAIL={fail} TOTAL={scenarios.Count}");
        return fail == 0 ? 0 : 1;
    }

    private static List<Scenario> BuildScenarios() => new()
    {
        new("S01", "WrapInstanceMethod",
            "在调用原方法基础上, 把返回值改成 [P] + 原结果大写",
            "Greet(\"alice\") == \"[P] HI ALICE\"",
            "ScenarioTargets/Scenarios/S01_WrapInstanceMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S01_WrapInstanceMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S01_WrapInstanceMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Greet")!.Invoke(inst, new object[] { "alice" })!;
                return new ScenarioResult(got, got == "[P] HI ALICE");
            }),

        new("S02", "ReplaceInstanceMethod",
            "完全替换方法体, 不调用原方法, 返回 x+100",
            "Score(5) == 105",
            "ScenarioTargets/Scenarios/S02_ReplaceInstanceMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S02_ReplaceInstanceMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S02_ReplaceInstanceMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Score")!.Invoke(inst, new object[] { 5 })!;
                return new ScenarioResult(got.ToString(), got == 105);
            }),

        new("S03", "WrapStaticMethod",
            "包装静态方法, 在原返回后追加 !",
            "Echo(\"hi\") == \"hi!\"",
            "ScenarioTargets/Scenarios/S03_WrapStaticMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S03_WrapStaticMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S03_WrapStaticMethod")!;
                var got = (string)t.GetMethod("Echo")!.Invoke(null, new object[] { "hi" })!;
                return new ScenarioResult(got, got == "hi!");
            }),

        new("S04", "PatchInstanceConstructor",
            "用 [MonoModConstructor] patch 实例构造函数, 调用 orig_ctor 后改写 Marker",
            "Marker == \"ctor:patched\"",
            "ScenarioTargets/Scenarios/S04_PatchInstanceConstructor.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S04_PatchInstanceConstructorPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S04_PatchInstanceConstructor")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetProperty("Marker")!.GetValue(inst)!;
                return new ScenarioResult(got, got == "ctor:patched");
            }),

        new("S05", "PatchStaticConstructor",
            "用 [MonoModConstructor] patch 静态构造函数, 调用 orig_cctor 后改写 StaticMarker",
            "StaticMarker == \"sctor:patched\"",
            "ScenarioTargets/Scenarios/S05_PatchStaticConstructor.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S05_PatchStaticConstructorPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S05_PatchStaticConstructor")!;
                var got = (string)t.GetProperty("StaticMarker")!.GetValue(null)!;
                return new ScenarioResult(got, got == "sctor:patched");
            }),

        new("S06", "AddNewMembers",
            "向已 patch 的类型新增字段/属性/方法, 且原方法保持不变",
            "ExtraField==\"extra\", ExtraProp==\"prop\", ExtraMethod()==\"extra-method\", Base()==\"base\"",
            "ScenarioTargets/Scenarios/S06_AddNewMembers.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S06_AddNewMembersPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S06_AddNewMembers")!;
                var inst = Activator.CreateInstance(t)!;
                var f = (string)t.GetField("ExtraField")!.GetValue(inst)!;
                var p = (string)t.GetProperty("ExtraProp")!.GetValue(inst)!;
                var m = (string)t.GetMethod("ExtraMethod")!.Invoke(inst, null)!;
                var b = (string)t.GetMethod("Base")!.Invoke(inst, null)!;
                var actual = $"field={f}; prop={p}; method={m}; base={b}";
                var ok = f == "extra" && p == "prop" && m == "extra-method" && b == "base";
                return new ScenarioResult(actual, ok);
            }),

        new("S07", "IgnoreHelperNotCalled",
            "[MonoModIgnore] 标记的辅助方法不应被复制进目标程序集, 且主方法仍被 patch",
            "Run()==\"run+patched\" 且 Helper 方法不存在于 modded 类型",
            "ScenarioTargets/Scenarios/S07_IgnoreHelper.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S07_IgnoreHelperPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S07_IgnoreHelper")!;
                var inst = Activator.CreateInstance(t)!;
                var run = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                var helper = t.GetMethod("Helper", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                var actual = $"run={run}; helper={(helper is null ? "absent" : "present")}";
                return new ScenarioResult(actual, run == "run+patched" && helper is null);
            }),

        new("S08", "ExplicitPatchAttribute",
            "patch 类位于不同命名空间, 用 [MonoModPatch(\"global::...\")] 显式指定目标类型",
            "Label() == \"label!\"",
            "ScenarioTargets/Scenarios/S08_ExplicitTarget.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S08_ExplicitTargetPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S08_ExplicitTarget")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Label")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "label!");
            }),

        new("S09", "RefParameter",
            "包装带 ref 参数的方法, 先调用 orig_ 再额外加 10",
            "Bump(ref 5) 后 x == 16",
            "ScenarioTargets/Scenarios/S09_RefParameter.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S09_RefParameterPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S09_RefParameter")!;
                var inst = Activator.CreateInstance(t)!;
                var args = new object[] { 5 };
                t.GetMethod("Bump")!.Invoke(inst, args);
                var got = (int)args[0];
                return new ScenarioResult(got.ToString(), got == 16);
            }),

        new("S10", "OutParameter",
            "包装带 out 参数的方法, 调用 orig_ 后把 out 值加 100",
            "TryGet(out r) 返回 true 且 r == 101",
            "ScenarioTargets/Scenarios/S10_OutParameter.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S10_OutParameterPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S10_OutParameter")!;
                var inst = Activator.CreateInstance(t)!;
                var args = new object[] { 0 };
                var ret = (bool)t.GetMethod("TryGet")!.Invoke(inst, args)!;
                var r = (int)args[0];
                var actual = $"ret={ret}; r={r}";
                return new ScenarioResult(actual, ret && r == 101);
            }),

        new("S11", "PropertyGetterSetter",
            "分别 patch 属性的 getter 和 setter, getter 在原值后追加 :get, setter 存入值+:set",
            "set \"x\" 后 get == \"x:set:get\"",
            "ScenarioTargets/Scenarios/S11_PropertyAccessors.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S11_PropertyAccessorsPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S11_PropertyAccessors")!;
                var inst = Activator.CreateInstance(t)!;
                var prop = t.GetProperty("Value")!;
                prop.SetValue(inst, "x");
                var got = (string)prop.GetValue(inst)!;
                return new ScenarioResult(got, got == "x:set:get");
            }),

        new("S12", "MethodOverloads",
            "只 patch 重载中的一个 Do(int), 另一个 Do(string) 保持不变",
            "Do(5)==\"int:5!\", Do(\"z\")==\"str:z\"",
            "ScenarioTargets/Scenarios/S12_MethodOverloads.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S12_MethodOverloadsPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S12_MethodOverloads")!;
                var inst = Activator.CreateInstance(t)!;
                var di = (string)t.GetMethod("Do", new[] { typeof(int) })!.Invoke(inst, new object[] { 5 })!;
                var ds = (string)t.GetMethod("Do", new[] { typeof(string) })!.Invoke(inst, new object[] { "z" })!;
                var actual = $"int={di}; str={ds}";
                return new ScenarioResult(actual, di == "int:5!" && ds == "str:z");
            }),

        new("S13", "PrivateMethodPatch",
            "patch 私有方法 Secret, 通过公共方法 Reveal 间接验证行为已改变",
            "Reveal() == \"secret!\"",
            "ScenarioTargets/Scenarios/S13_PrivateMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S13_PrivateMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S13_PrivateMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Reveal")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "secret!");
            }),

        new("S14", "ReplaceModifier",
            "用 [MonoModReplace] 完全替换方法体且不生成 orig_ 副本",
            "Mode()==\"fast\" 且类型上不存在 orig_Mode 方法",
            "ScenarioTargets/Scenarios/S14_ReplaceModifier.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S14_ReplaceModifierPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S14_ReplaceModifier")!;
                var inst = Activator.CreateInstance(t)!;
                var mode = (string)t.GetMethod("Mode")!.Invoke(inst, null)!;
                var orig = t.GetMethod("orig_Mode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var actual = $"mode={mode}; origMode={(orig is null ? "absent" : "present")}";
                return new ScenarioResult(actual, mode == "fast" && orig is null);
            }),

        new("S15", "GenericMethod",
            "包装泛型方法, 在原返回外层加方括号",
            "Format<int>(7) == \"[fmt:7]\"",
            "ScenarioTargets/Scenarios/S15_GenericMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S15_GenericMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S15_GenericMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Format")!.MakeGenericMethod(typeof(int));
                var got = (string)mi.Invoke(inst, new object[] { 7 })!;
                return new ScenarioResult(got, got == "[fmt:7]");
            }),

        new("S16", "NestedType",
            "patch 嵌套类型, 在原返回后追加 !",
            "S16_NestedOwner.Inner.Id() == \"inner!\"",
            "ScenarioTargets/Scenarios/S16_NestedOwner.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S16_NestedOwnerPatch.cs",
            a =>
            {
                var inner = a.GetType("MonoModTestTargets.S16_NestedOwner+Inner")!;
                var inst = Activator.CreateInstance(inner)!;
                var got = (string)inner.GetMethod("Id")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "inner!");
            }),

        new("S17", "ExceptionSwallow",
            "包装会抛异常的方法, 捕获后返回安全值",
            "Risky() == \"safe\" (原方法抛 InvalidOperationException)",
            "ScenarioTargets/Scenarios/S17_ExceptionSource.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S17_ExceptionSourcePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S17_ExceptionSource")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Risky")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "safe");
            }),

        new("S18", "ParamsArray",
            "包装 params string[] 方法, 在原返回后追加 !",
            "Join(\"a\",\"b\") == \"a,b!\"",
            "ScenarioTargets/Scenarios/S18_ParamsArray.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S18_ParamsArrayPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S18_ParamsArray")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Join")!.Invoke(inst, new object[] { new string[] { "a", "b" } })!;
                return new ScenarioResult(got, got == "a,b!");
            }),

        new("S20", "InheritedMethodPatch",
            "patch 基类方法, 派生类继承的调用也应体现补丁行为",
            "new S20_Derived().Who() == \"base!\"",
            "ScenarioTargets/Scenarios/S20_InheritedMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S20_InheritedMethodPatch.cs",
            a =>
            {
                var d = a.GetType("MonoModTestTargets.S20_Derived")!;
                var inst = Activator.CreateInstance(d)!;
                var got = (string)d.GetMethod("Who")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "base!");
            }),

        new("S21", "OriginalNameAttribute",
            "用 [MonoModOriginal] + [MonoModOriginalName] 自定义原方法名, 包装原方法并追加 !",
            "Code() == \"c!\"",
            "ScenarioTargets/Scenarios/S21_OriginalName.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S21_OriginalNamePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S21_OriginalName")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Code")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "c!");
            }),

        new("S22", "RemoveMember",
            "用 [MonoModRemove] 把 Extra() 方法从目标类型移除, Keep() 保持不变",
            "Extra 方法不存在于 modded 类型, Keep()==\"keep\"",
            "ScenarioTargets/Scenarios/S22_RemoveMember.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S22_RemoveMemberPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S22_RemoveMember")!;
                var inst = Activator.CreateInstance(t)!;
                var keep = (string)t.GetMethod("Keep")!.Invoke(inst, null)!;
                var extra = t.GetMethod("Extra");
                var actual = $"keep={keep}; extra={(extra is null ? "absent" : "present")}";
                return new ScenarioResult(actual, keep == "keep" && extra is null);
            }),

        new("S23", "MonoModPublicMember",
            "用 [MonoModPublic] 把目标 internal 方法在补丁后变为 public, 并包装返回值追加 !",
            "Hidden()==\"hidden!\" 且 Hidden 在 modded 类型上为 public",
            "ScenarioTargets/Scenarios/S23_MonoModPublic.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S23_MonoModPublicPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S23_MonoModPublic")!;
                var inst = Activator.CreateInstance(t)!;
                var m = t.GetMethod("Hidden")!;
                var got = (string)m.Invoke(inst, null)!;
                var actual = $"hidden={got}; isPublic={m.IsPublic}";
                return new ScenarioResult(actual, got == "hidden!" && m.IsPublic);
            }),

        new("S24", "NoNewSkipsAbsentMethod",
            "[MonoModNoNew] 标记的方法在目标中不存在时应被跳过, 主方法仍被 patch",
            "Exists()==\"yes!\" 且 NotInTarget 不存在于 modded 类型",
            "ScenarioTargets/Scenarios/S24_NoNew.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S24_NoNewPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S24_NoNew")!;
                var inst = Activator.CreateInstance(t)!;
                var exists = (string)t.GetMethod("Exists")!.Invoke(inst, null)!;
                var notIn = t.GetMethod("NotInTarget");
                var actual = $"exists={exists}; notInTarget={(notIn is null ? "absent" : "present")}";
                return new ScenarioResult(actual, exists == "yes!" && notIn is null);
            }),

        new("S25", "VirtualOverridePatch",
            "patch 派生类的 override 方法, 仅派生类调用体现补丁, 基类调用不变",
            "new S25_Derived().Virt()==\"derived!\", new S25_Base().Virt()==\"base-virt\"",
            "ScenarioTargets/Scenarios/S25_VirtualOverride.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S25_VirtualOverridePatch.cs",
            a =>
            {
                var d = a.GetType("MonoModTestTargets.S25_Derived")!;
                var b = a.GetType("MonoModTestTargets.S25_Base")!;
                var di = Activator.CreateInstance(d)!;
                var bi = Activator.CreateInstance(b)!;
                var dg = (string)d.GetMethod("Virt")!.Invoke(di, null)!;
                var bg = (string)b.GetMethod("Virt")!.Invoke(bi, null)!;
                var actual = $"derived={dg}; base={bg}";
                return new ScenarioResult(actual, dg == "derived!" && bg == "base-virt");
            }),

        new("S26", "VoidSideEffectWrap",
            "包装 void 方法, 调用 orig_ 后额外累加 10, 验证原方法仅执行一次",
            "重置 Count 后 Tick() 一次 Count == 11",
            "ScenarioTargets/Scenarios/S26_VoidSideEffect.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S26_VoidSideEffectPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S26_VoidSideEffect")!;
                var countProp = t.GetProperty("Count", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var countField = t.GetField("Count", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (countField is null) return new ScenarioResult("Count field missing", false);
                countField.SetValue(null, 0);
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Tick")!.Invoke(inst, null);
                var got = (int)countField.GetValue(null)!;
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S28", "GenericTypePatch",
            "patch 泛型类型 S28_Box<T> 的方法, 在原返回后追加 !",
            "new S28_Box<int>().Show() == \"Int32!\"",
            "ScenarioTargets/Scenarios/S28_GenericType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S28_GenericTypePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S28_Box`1")!.MakeGenericType(typeof(int));
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Show")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "Int32!");
            }),

        new("S30", "OptionalParameter",
            "包装带默认参数的方法, 默认值语义保持, 返回值追加 !",
            "Greet(\"hi\") == \"hi.!\"",
            "ScenarioTargets/Scenarios/S30_OptionalParameter.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S30_OptionalParameterPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S30_OptionalParameter")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Greet")!.Invoke(inst, new object[] { "hi", Type.Missing })!;
                return new ScenarioResult(got, got == "hi.!");
            }),

        new("S31", "InParameter",
            "包装带 in 参数的方法, 调用 orig_ 后再加 10",
            "Add(5) == 16",
            "ScenarioTargets/Scenarios/S31_InParameter.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S31_InParameterPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S31_InParameter")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Add")!;
                var args = new object[] { 5 };
                var got = (int)mi.Invoke(inst, args)!;
                return new ScenarioResult(got.ToString(), got == 16);
            }),

        new("S34", "MultiplePatchesSameType",
            "两个不同 patch_ 类型分别 patch 同一目标类型的 A 和 B 方法, 一次性补丁应同时生效",
            "A()==\"a!A\", B()==\"b!B\"",
            "ScenarioTargets/Scenarios/S34_MultiplePatchesSameType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S34_MultiplePatchesSameType_APatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S34_Multi")!;
                var inst = Activator.CreateInstance(t)!;
                var av = (string)t.GetMethod("A")!.Invoke(inst, null)!;
                var bv = (string)t.GetMethod("B")!.Invoke(inst, null)!;
                var actual = $"a={av}; b={bv}";
                return new ScenarioResult(actual, av == "a!A" && bv == "b!B");
            }),

        new("S35", "StructMethodPatch",
            "patch 值类型(struct)的方法, 用 [MonoModPatch] 显式指定目标类型(因 struct 无法继承), 在原返回上加 1",
            "new S35_Point{X=5}.Twice() == 11",
            "ScenarioTargets/Scenarios/S35_StructMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S35_StructMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S35_Point")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetField("X")!.SetValue(inst, 5);
                var got = (int)t.GetMethod("Twice")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S27", "NullReturnReplacement",
            "原方法返回 null, 用 [MonoModReplace] 替换为非空值",
            "Maybe() == \"not-null\"",
            "ScenarioTargets/Scenarios/S27_NullReturnReplacement.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S27_NullReturnReplacementPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S27_NullReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Maybe")!.Invoke(inst, null)!;
                return new ScenarioResult(got ?? "<null>", got == "not-null");
            }),

        new("S29", "ReplaceConstructorBody",
            "用 [MonoModReplace]+[MonoModConstructor] 完全替换实例构造函数体, 不调用原 ctor",
            "new S29().Tag == \"replaced\"",
            "ScenarioTargets/Scenarios/S29_ReplaceConstructor.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S29_ReplaceConstructorPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S29_ReplaceConstructor")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetProperty("Tag")!.GetValue(inst)!;
                return new ScenarioResult(got, got == "replaced");
            }),

        new("S36", "IgnoredHelperType",
            "[MonoModIgnore] 标记的整个辅助类型不应被复制进目标程序集, 主方法仍被 patch",
            "Run()==\"run+p\" 且 S36_Helpers 类型不存在于 modded 程序集",
            "ScenarioTargets/Scenarios/S36_IgnoredHelperType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S36_IgnoredHelperTypePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S36_IgnoredHelperType")!;
                var inst = Activator.CreateInstance(t)!;
                var run = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                var helperType = a.GetType("MonoModTestTargets.S36_Helpers");
                var actual = $"run={run}; helperType={(helperType is null ? "absent" : "present")}";
                return new ScenarioResult(actual, run == "run+p" && helperType is null);
            }),

        new("S38", "AddNewConstructorOverload",
            "向目标类型新增一个带参构造函数重载, 并在内部初始化新增字段",
            "new S38(\"hi\").Note == \"hi\"",
            "ScenarioTargets/Scenarios/S38_AddNewConstructor.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S38_AddNewConstructorPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S38_AddNewConstructor")!;
                var inst = Activator.CreateInstance(t, new object[] { "hi" })!;
                var got = (string)t.GetField("Note")!.GetValue(inst)!;
                return new ScenarioResult(got, got == "hi");
            }),

        new("S39", "SealedClassPatch",
            "patch 密封类方法: 密封类无法继承, 用 [MonoModPatch] 显式指定目标类型(不继承), 在原返回后追加 !",
            "new S39_SealedClass().Name() == \"sealed!\"",
            "ScenarioTargets/Scenarios/S39_SealedClassPatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S39_SealedClassPatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S39_SealedClass")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Name")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "sealed!");
            }),

        new("S41", "EventRaiseWrap",
            "包装会触发事件的方法, orig_ 仍正确触发事件, 补丁额外累加 Hits",
            "订阅 handler(Hits+=5) 后 Fire() 一次 Hits == 6",
            "ScenarioTargets/Scenarios/S41_EventRaisePatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S41_EventRaisePatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S41_EventRaise")!;
                var inst = Activator.CreateInstance(t)!;
                var ev = t.GetEvent("Done")!;
                System.EventHandler handler = (s, e) =>
                {
                    t.GetField("Hits")!.SetValue(s, (int)t.GetField("Hits")!.GetValue(s)! + 5);
                };
                ev.AddEventHandler(inst, handler);
                t.GetMethod("Fire")!.Invoke(inst, null);
                var hits = (int)t.GetField("Hits")!.GetValue(inst)!;
                return new ScenarioResult(hits.ToString(), hits == 6);
            }),

        new("S42", "ReplacePropertyQuirk",
            "对 patch 中声明为只读的属性施加 [MonoModReplace]: 验证 MonoMod 的实际结构行为 (原属性元数据与 setter/backing 被移除, patch 的 getter 作为独立 get_Label 方法保留并返回新值)",
            "get_Label() == \"replaced\"; Label 属性元数据不存在; set_Label 不存在",
            "ScenarioTargets/Scenarios/S42_ReplacePropertyAccessor.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S42_ReplacePropertyAccessorPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S42_ReplaceProperty")!;
                var inst = Activator.CreateInstance(t)!;
                var prop = t.GetProperty("Label");
                var getter = t.GetMethod("get_Label");
                var setter = t.GetMethod("set_Label", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string getterVal = getter is null ? "<no-getter>" : (string)getter.Invoke(inst, null)!;
                var actual = $"prop={(prop is null ? "absent" : "present")}; get_Label={(getter is null ? "absent" : "present")}; set_Label={(setter is null ? "absent" : "present")}; getVal={getterVal}";
                return new ScenarioResult(actual, prop is null && getter is not null && getterVal == "replaced" && setter is null);
            }),

        new("S43", "AsyncMethodWrap",
            "包装 async Task<string> 方法, await orig_ 后在结果追加 !",
            "FetchAsync().Result == \"fetched!\"",
            "ScenarioTargets/Scenarios/S43_AsyncMethodPatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S43_AsyncMethodPatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S43_AsyncMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var task = (System.Threading.Tasks.Task<string>)t.GetMethod("FetchAsync")!.Invoke(inst, null)!;
                var got = task.GetAwaiter().GetResult();
                return new ScenarioResult(got, got == "fetched!");
            }),

        new("S44", "RefReturnWrap",
            "包装 ref 返回方法, 通过 orig_ 拿到 ref 后修改底层值并返回",
            "Slot() 后再读 _v 字段 == 101",
            "ScenarioTargets/Scenarios/S44_RefReturnMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S44_RefReturnMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S44_RefReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Slot")!;
                // Invoke returns the boxed ref value; we instead read the backing field.
                mi.Invoke(inst, null);
                var backing = t.GetField("_v", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var got = (int)backing.GetValue(inst)!;
                return new ScenarioResult(got.ToString(), got == 101);
            }),

        new("S45", "InterfaceImplPatch",
            "patch 实现接口的类型的虚方法, 通过接口引用调用也应体现补丁",
            "((S45_IShape)new S45_Circle()).Draw() == \"[circle]\"",
            "ScenarioTargets/Scenarios/S45_InterfaceImplPatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S45_InterfaceImplPatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S45_Circle")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Draw")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "[circle]");
            }),

        new("S46", "RecursiveMethodReentrantWrap",
            "包装递归方法 Fact: orig_ 会重新进入已被 patch 的 Fact, 形成 reentrant 包装 (每层递归都被补丁拦截), 结果偏离原 6 并体现多层包装叠加; 这是 MonoMod orig_ 对递归方法的行为特征",
            "Fact(3) != 6 (原值) 且 == 16 (多层 reentrant 叠加)",
            "ScenarioTargets/Scenarios/S46_RecursiveMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S46_RecursiveMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S46_Recursive")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Fact")!.Invoke(inst, new object[] { 3 })!;
                return new ScenarioResult(got.ToString(), got == 16);
            }),

        new("S47", "StaticGenericMethodPatch",
            "包装静态泛型方法 Identity<T>, 在原返回后追加 !",
            "Identity<int>(7) == \"id:7!\"",
            "ScenarioTargets/Scenarios/S47_StaticGenericMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S47_StaticGenericMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S47_StaticGeneric")!;
                var mi = t.GetMethod("Identity")!.MakeGenericMethod(typeof(int));
                var got = (string)mi.Invoke(null, new object[] { 7 })!;
                return new ScenarioResult(got, got == "id:7!");
            }),

        new("S48", "GenericConstraintMethodPatch",
            "包装带泛型约束 (where T: IEquatable<T>) 的方法, 约束保持, 追加 !",
            "Show<int>(9) == \"c:9!\"",
            "ScenarioTargets/Scenarios/S48_GenericConstraintMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S48_GenericConstraintMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S48_Constraint")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Show")!.MakeGenericMethod(typeof(int));
                var got = (string)mi.Invoke(inst, new object[] { 9 })!;
                return new ScenarioResult(got, got == "c:9!");
            }),

        new("S49", "RemoveMemberSafe",
            "用 [MonoModRemove] 移除一个无任何方法体引用的成员方法, 保留 Keep() 不变",
            "Extra 方法不存在于 modded 类型, Keep()==\"keep\"",
            "ScenarioTargets/Scenarios/S49_RemoveField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S49_RemoveFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S49_RemoveMember")!;
                var inst = Activator.CreateInstance(t)!;
                var keep = (string)t.GetMethod("Keep")!.Invoke(inst, null)!;
                var extra = t.GetMethod("Extra");
                var actual = $"keep={keep}; extra={(extra is null ? "absent" : "present")}";
                return new ScenarioResult(actual, keep == "keep" && extra is null);
            }),

        new("S50", "ArrayReturnWrap",
            "包装返回数组的方法, 调用 orig_ 后追加一个元素 3",
            "Pair() 序列 == [1,2,3]",
            "ScenarioTargets/Scenarios/S50_ReplaceField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S50_ReplaceFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S50_ArrayReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int[])t.GetMethod("Pair")!.Invoke(inst, null)!;
                var actual = string.Join(",", got);
                return new ScenarioResult(actual, got.Length == 3 && got[0] == 1 && got[1] == 2 && got[2] == 3);
            }),

        new("S51", "IndexerAccessorPatch",
            "分别 patch 索引器的 getter/setter (orig_get_Item / orig_set_Item), get 追加 !, set 写入时附加 #",
            "set[0]=\"x\" 后 get[0] == \"x#!\"",
            "ScenarioTargets/Scenarios/S51_IndexerPatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S51_IndexerPatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S51_Indexer")!;
                var inst = Activator.CreateInstance(t)!;
                var idx = t.GetProperty("Item")!;
                idx.SetValue(inst, "x", new object[] { 0 });
                var got = (string)idx.GetValue(inst, new object[] { 0 })!;
                return new ScenarioResult(got, got == "x#!");
            }),

        new("S52", "StaticFieldReadWrap",
            "包装读取静态字段的静态方法, 在原返回上加 1000",
            "Counter=0 时 ReadCounter() == 1000",
            "ScenarioTargets/Scenarios/S52_StaticFieldWrap.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S52_StaticFieldWrapPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S52_StaticFieldWrap")!;
                t.GetField("Counter")!.SetValue(null, 0);
                var got = (int)t.GetMethod("ReadCounter")!.Invoke(null, null)!;
                return new ScenarioResult(got.ToString(), got == 1000);
            }),

        new("S53", "AddPublicAlongsideExplicitInterface",
            "目标类型有显式接口实现 IComparable.CompareTo, patch 向类型新增一个公共 CompareTo 返回固定值, 验证新增公共方法可用且显式接口实现不受影响",
            "新公共 CompareTo(null)==42, 接口路由 ((IComparable)obj).CompareTo(null)==0",
            "ScenarioTargets/Scenarios/S53_ExplicitInterfaceImpl.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S53_ExplicitInterfaceImplPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S53_ExplicitInterface")!;
                var inst = Activator.CreateInstance(t)!;
                var pub = t.GetMethod("CompareTo", new[] { typeof(object) })!;
                var pubGot = (int)pub.Invoke(inst, new object?[] { null })!;
                var ifaceGot = ((System.IComparable)inst).CompareTo(null);
                var actual = $"public={pubGot}; iface={ifaceGot}";
                return new ScenarioResult(actual, pubGot == 42 && ifaceGot == 0);
            }),

        new("S54", "ReadonlyStructMethodPatch",
            "patch readonly struct 的方法, 用 [MonoModPatch] 不继承写法 (readonly struct 不可继承), 在原返回上加 1",
            "new S54_ReadonlyStruct(5).Double() == 11",
            "ScenarioTargets/Scenarios/S54_ReadonlyStructMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S54_ReadonlyStructMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S54_ReadonlyStruct")!;
                var inst = Activator.CreateInstance(t, new object[] { 5 })!;
                var got = (int)t.GetMethod("Double")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S55", "NullableReturnWrap",
            "包装返回 Nullable<int> 的方法, 原返回非空时加 100, 原返回 null 时仍返回 null",
            "Find(3)==103, Find(0)==null",
            "ScenarioTargets/Scenarios/S55_NullableReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S55_NullableReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S55_NullableReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Find")!;
                var r3 = (int?)mi.Invoke(inst, new object[] { 3 });
                var r0 = (int?)mi.Invoke(inst, new object[] { 0 });
                var actual = $"find(3)={r3}; find(0)={(r0 is null ? "null" : r0.ToString())}";
                return new ScenarioResult(actual, r3 == 103 && r0 is null);
            }),

        new("S57", "EnumArgMethodPatch",
            "包装带枚举参数的方法, 在原返回后追加 !",
            "Name(S57_Color.Green) == \"color:Green!\"",
            "ScenarioTargets/Scenarios/S57_EnumArgMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S57_EnumArgMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S57_EnumArg")!;
                var colorType = a.GetType("MonoModTestTargets.S57_Color")!;
                var inst = Activator.CreateInstance(t)!;
                var green = Enum.Parse(colorType, "Green");
                var got = (string)t.GetMethod("Name")!.Invoke(inst, new object[] { green })!;
                return new ScenarioResult(got, got == "color:Green!");
            }),

        new("S58", "CrossNamespacePatchType",
            "patch_ 类型位于与目标不同的命名空间, MonoMod 按 patch_ 前缀剥离后的简单名映射目标类型",
            "new S58_CrossNamespace().Tag() == \"sub!\"",
            "ScenarioTargets/Scenarios/S58_CrossNamespaceType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S58_CrossNamespaceTypePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.SubNs.S58_CrossNamespace")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Tag")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "sub!");
            }),

        new("S59", "MultiTypeParamGenericMethod",
            "包装带两个泛型参数的方法 Pair<T,U>, 在原返回外加方括号",
            "Pair<int,string>(7,\"z\") == \"[Int32+String:7,z]\"",
            "ScenarioTargets/Scenarios/S59_MultiTypeParamGeneric.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S59_MultiTypeParamGenericPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S59_MultiTypeParam")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Pair")!.MakeGenericMethod(typeof(int), typeof(string));
                var got = (string)mi.Invoke(inst, new object[] { 7, "z" })!;
                return new ScenarioResult(got, got == "[Int32+String:7,z]");
            }),

        new("S60", "DecimalReturnWrap",
            "包装 decimal 返回方法, 在原结果上加 1m",
            "Total(2m, 3m) == 6m",
            "ScenarioTargets/Scenarios/S60_DecimalReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S60_DecimalReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S60_DecimalReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (decimal)t.GetMethod("Total")!.Invoke(inst, new object[] { 2m, 3m })!;
                return new ScenarioResult(got.ToString(), got == 6m);
            }),

        new("S61", "CopiedHelperCallable",
            "patch 中未标记 [MonoModIgnore] 的辅助方法会被复制进目标类型, 补丁方法体可调用它 (与 S07 IgnoreHelper 对比)",
            "Run()==\"run+copied\" 且 Suffix 方法存在于 modded 类型",
            "ScenarioTargets/Scenarios/S61_CopiedHelper.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S61_CopiedHelperPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S61_CopiedHelper")!;
                var inst = Activator.CreateInstance(t)!;
                var run = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                var suffix = t.GetMethod("Suffix");
                var actual = $"run={run}; suffix={(suffix is null ? "absent" : "present")}";
                return new ScenarioResult(actual, run == "run+copied" && suffix is not null);
            }),

        new("S62", "AddStaticFieldInitInCtor",
            "向目标类型新增 static 字段, 并在 patch 构造函数中惰性初始化 (仅首次)",
            "首次 new S62() 后 GlobalTag == \"init\", 再次 new 仍 == \"init\"",
            "ScenarioTargets/Scenarios/S62_AddStaticField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S62_AddStaticFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S62_AddStaticField")!;
                var f = t.GetField("GlobalTag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (f is null) return new ScenarioResult("GlobalTag missing", false);
                f.SetValue(null, null);
                Activator.CreateInstance(t);
                var first = (string?)f.GetValue(null);
                Activator.CreateInstance(t);
                var second = (string?)f.GetValue(null);
                var actual = $"first={first}; second={second}";
                return new ScenarioResult(actual, first == "init" && second == "init");
            }),

        new("S63", "LinkFromStaticRelink",
            "用 [MonoModLinkFrom] 静态重链接: 目标 Wrap() 内部调用 Old(), 补丁提供 Replacement() 并声明 LinkFrom Old() 的 findableID, 使 Wrap 内对 Old 的调用被重定向到 Replacement",
            "new S63_LinkFrom().Wrap() == \"relinked\"",
            "ScenarioTargets/Scenarios/S63_LinkFromRelink.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S63_LinkFromRelinkPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S63_LinkFrom")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Wrap")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "relinked");
            }),

        new("S64", "OperatorOverloadPatch",
            "patch 运算符重载方法 op_Addition, 在原结果上加 1",
            "(new S64(2) + new S64(3)).Value == 6",
            "ScenarioTargets/Scenarios/S64_OperatorOverload.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S64_OperatorOverloadPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S64_OperatorOverload")!;
                var a1 = Activator.CreateInstance(t, new object[] { 2 })!;
                var a2 = Activator.CreateInstance(t, new object[] { 3 })!;
                var op = t.GetMethod("op_Addition")!;
                var sum = op.Invoke(null, new object[] { a1, a2 })!;
                var got = (int)t.GetField("Value")!.GetValue(sum)!;
                return new ScenarioResult(got.ToString(), got == 6);
            }),

        new("S65", "FuncReturnWrap",
            "包装返回 Func<int,int> 的方法, 拿到原委托后返回新委托 (在原结果上加 1)",
            "Getter()(5) == 11",
            "ScenarioTargets/Scenarios/S65_FuncReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S65_FuncReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S65_FuncReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var f = (System.Func<int, int>)t.GetMethod("Getter")!.Invoke(inst, null)!;
                var got = f(5);
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S67", "ParamsObjectArrayWrap",
            "包装 params object[] 方法, 在原返回后追加 !",
            "Join(1,\"x\",true) == \"1,x,True!\"",
            "ScenarioTargets/Scenarios/S67_ParamsObjectArray.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S67_ParamsObjectArrayPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S67_ParamsObjectArray")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Join")!.Invoke(inst, new object[] { new object[] { 1, "x", true } })!;
                return new ScenarioResult(got, got == "1,x,True!");
            }),

        new("S68", "TryFinallyCleanup",
            "包装方法用 try/finally, 验证 orig_ 正常返回后 finally 中的清理逻辑执行",
            "Render()==\"render!\" 且 CleanedUp==true",
            "ScenarioTargets/Scenarios/S68_TryFinallyWrap.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S68_TryFinallyWrapPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S68_TryFinallyWrap")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Render")!.Invoke(inst, null)!;
                var cleaned = (bool)t.GetField("CleanedUp")!.GetValue(inst)!;
                var actual = $"render={got}; cleaned={cleaned}";
                return new ScenarioResult(actual, got == "render!" && cleaned);
            }),

        new("S69", "PatchThrowsException",
            "patch 在调用 orig_ 后抛出特定异常, 验证补丁方法体异常语义生效",
            "Go() 抛 InvalidOperationException 且消息以 \"patched:ok\" 开头",
            "ScenarioTargets/Scenarios/S69_ThrowsException.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S69_ThrowsExceptionPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S69_ThrowsException")!;
                var inst = Activator.CreateInstance(t)!;
                string actual;
                bool ok;
                try
                {
                    t.GetMethod("Go")!.Invoke(inst, null);
                    actual = "no-throw";
                    ok = false;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is System.InvalidOperationException ioe)
                {
                    actual = "threw:" + ioe.Message;
                    ok = ioe.Message.StartsWith("patched:ok");
                }
                return new ScenarioResult(actual, ok);
            }),

        new("S70", "TypeIdentityPatch",
            "patch 调用 orig_(其内部 GetType().Name) 并追加 !, 验证 GetType 在 patched 类型上返回目标类型名",
            "TypeName() == \"S70_TypeIdentity!\"",
            "ScenarioTargets/Scenarios/S70_TypeIdentity.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S70_TypeIdentityPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S70_TypeIdentity")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("TypeName")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "S70_TypeIdentity!");
            }),

        new("S71", "ObjectArgMethodWrap",
            "包装 object 参数方法, 在原返回后追加 !",
            "Describe(42) == \"obj:42!\"",
            "ScenarioTargets/Scenarios/S71_ObjectArgMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S71_ObjectArgMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S71_ObjectArgMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Describe")!.Invoke(inst, new object[] { 42 })!;
                return new ScenarioResult(got, got == "obj:42!");
            }),

        new("S72", "SelfAddedMethodInvoke",
            "patch 方法体调用同一 patch 新增的实例方法 Extra(), 验证新增方法被复制且可被补丁方法体调用",
            "Base() == \"base:extra\"",
            "ScenarioTargets/Scenarios/S72_SelfAddedMethodInvoke.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S72_SelfAddedMethodInvokePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S72_SelfAddedMethodInvoke")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Base")!.Invoke(inst, null)!;
                var extra = t.GetMethod("Extra");
                var actual = $"base={got}; extra={(extra is null ? "absent" : "present")}";
                return new ScenarioResult(actual, got == "base:extra" && extra is not null);
            }),

        new("S73", "RethrowWrappedException",
            "patch 用 try/catch 包装 orig_, 捕获原 FormatException 后重新抛出带前缀消息的新异常",
            "Parse(\"abc\") 抛 FormatException 且消息以 \"wrapped:\" 开头; Parse(\"42\")==42",
            "ScenarioTargets/Scenarios/S73_RethrowWrap.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S73_RethrowWrapPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S73_RethrowWrap")!;
                var inst = Activator.CreateInstance(t)!;
                var ok42 = (int)t.GetMethod("Parse")!.Invoke(inst, new object[] { "42" })! == 42;
                string actual;
                bool okThrow;
                try
                {
                    t.GetMethod("Parse")!.Invoke(inst, new object[] { "abc" });
                    actual = "no-throw";
                    okThrow = false;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is System.FormatException fe)
                {
                    actual = "threw:" + fe.Message;
                    okThrow = fe.Message.StartsWith("wrapped:");
                }
                return new ScenarioResult($"parse42={ok42}; {actual}", ok42 && okThrow);
            }),

        new("S74", "StaticReadonlyFieldWrap",
            "包装读取 static readonly 字段的方法, 在原返回后追加 !",
            "Reveal() == \"topsecret!\"",
            "ScenarioTargets/Scenarios/S74_StaticReadonlyField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S74_StaticReadonlyFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S74_StaticReadonlyField")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Reveal")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "topsecret!");
            }),

        new("S75", "AddedMethodMutatesExistingField",
            "patch 新增实例方法 Bump(int), 调用它修改目标类型上已存在的字段 Count",
            "Bump(5) 后 Bump(3) 后 Count == 8",
            "ScenarioTargets/Scenarios/S75_AddedMethodMutatesField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S75_AddedMethodMutatesFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S75_AddedMethodMutatesField")!;
                var inst = Activator.CreateInstance(t)!;
                var bump = t.GetMethod("Bump");
                if (bump is null) return new ScenarioResult("Bump missing", false);
                bump.Invoke(inst, new object[] { 5 });
                bump.Invoke(inst, new object[] { 3 });
                var got = (int)t.GetField("Count")!.GetValue(inst)!;
                return new ScenarioResult(got.ToString(), got == 8);
            }),

        new("S76", "StringInterpolationWrap",
            "包装使用字符串插值的方法, 在原返回外加方括号",
            "Build(\"x\", 9) == \"[x-9]\"",
            "ScenarioTargets/Scenarios/S76_StringInterpolation.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S76_StringInterpolationPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S76_StringInterpolation")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Build")!.Invoke(inst, new object[] { "x", 9 })!;
                return new ScenarioResult(got, got == "[x-9]");
            }),

        new("S77", "ConditionalOrigCall",
            "patch 按条件决定是否调用 orig_: 空输入短路返回 \"empty\", 非空才调用 orig_ 并追加 !",
            "Echo(\"\")==\"empty\", Echo(\"hi\")==\"hi!\"",
            "ScenarioTargets/Scenarios/S77_ConditionalOrigCall.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S77_ConditionalOrigCallPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S77_ConditionalOrigCall")!;
                var inst = Activator.CreateInstance(t)!;
                var e0 = (string)t.GetMethod("Echo")!.Invoke(inst, new object[] { "" })!;
                var e1 = (string)t.GetMethod("Echo")!.Invoke(inst, new object[] { "hi" })!;
                var actual = $"empty={e0}; hi={e1}";
                return new ScenarioResult(actual, e0 == "empty" && e1 == "hi!");
            }),

        new("S78", "ValueTypeReturnWrap",
            "包装返回自定义 struct 的方法, 修改 struct 字段后返回",
            "Build().Code == 11 (原值 1)",
            "ScenarioTargets/Scenarios/S78_RulesCustomAttribute.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S78_RulesCustomAttributePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S78_ValueTypeReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = t.GetMethod("Build")!.Invoke(inst, null)!;
                var code = (int)t.Assembly.GetType("MonoModTestTargets.S78_Result")!.GetField("Code")!.GetValue(got)!;
                return new ScenarioResult(code.ToString(), code == 11);
            }),

        new("S79", "ListReturnWrap",
            "包装返回 List<int> 的方法, 调用 orig_ 后向列表追加元素 4",
            "Three() 序列 == [1,2,3,4]",
            "ScenarioTargets/Scenarios/S79_ListReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S79_ListReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S79_ListReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (System.Collections.Generic.List<int>)t.GetMethod("Three")!.Invoke(inst, null)!;
                var actual = string.Join(",", got);
                return new ScenarioResult(actual, got.Count == 4 && got[0] == 1 && got[1] == 2 && got[2] == 3 && got[3] == 4);
            }),

        new("S80", "ConstFieldAddCopied",
            "patch 新增 const 字段, 验证 const 值作为元数据被复制 (与普通字段初始化器不复制形成对比)",
            "Label() == \"label:EXTRA\"",
            "ScenarioTargets/Scenarios/S80_ConstFieldAdd.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S80_ConstFieldAddPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S80_ConstFieldAdd")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Label")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "label:EXTRA");
            }),

        new("S81", "TupleReturnWrap",
            "包装返回值元组 (int,string) 的方法, 对各分量分别变换",
            "Pair() == (11, \"x!\")",
            "ScenarioTargets/Scenarios/S81_TupleReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S81_TupleReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S81_TupleReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = ((int a, string b))t.GetMethod("Pair")!.Invoke(inst, null)!;
                var actual = $"a={got.a}; b={got.b}";
                return new ScenarioResult(actual, got.a == 11 && got.b == "x!");
            }),

        new("S82", "PrivateStaticMethodPatch",
            "patch 私有静态方法 Secret, 通过公共方法 Reveal 间接验证 (与 S13 私有实例方法互补, 这次是 static)",
            "Reveal() == \"secret!\"",
            "ScenarioTargets/Scenarios/S82_PrivateStaticMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S82_PrivateStaticMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S82_PrivateStaticMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Reveal")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "secret!");
            }),

        new("S83", "IEnumerableYieldWrap",
            "包装返回 IEnumerable<int> 的方法, 用 yield return 对每个元素加 100",
            "Range() 序列 == [101,102,103]",
            "ScenarioTargets/Scenarios/S83_IEnumerableReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S83_IEnumerableReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S83_IEnumerableReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (System.Collections.Generic.IEnumerable<int>)t.GetMethod("Range")!.Invoke(inst, null)!;
                var arr = got.ToArray();
                var actual = string.Join(",", arr);
                return new ScenarioResult(actual, arr.Length == 3 && arr[0] == 101 && arr[1] == 102 && arr[2] == 103);
            }),

        new("S84", "GenericParamsMethodWrap",
            "包装同时带泛型参数和 params 数组的方法, 在原返回外加方括号",
            "Compose<int>(\"p\", 1, 2) == \"[p:1,2]\"",
            "ScenarioTargets/Scenarios/S84_GenericParamsMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S84_GenericParamsMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S84_GenericParamsMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var mi = t.GetMethod("Compose")!.MakeGenericMethod(typeof(int));
                var got = (string)mi.Invoke(inst, new object[] { "p", new int[] { 1, 2 } })!;
                return new ScenarioResult(got, got == "[p:1,2]");
            }),

        new("S85", "NestedPrivateTypePatch",
            "patch 私有嵌套类型 Inner 的方法, 通过公共方法 Access 间接验证",
            "Access() == \"inner!\"",
            "ScenarioTargets/Scenarios/S85_NestedPrivateType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S85_NestedPrivateTypePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S85_NestedPrivateOwner")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Access")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "inner!");
            }),

        new("S86", "LockStatementWrap",
            "包装含 lock 语句的方法, orig_ 内部的 lock 仍正确执行, 补丁在外层加 1",
            "Run() == 43 (原值 42)",
            "ScenarioTargets/Scenarios/S86_LockStatement.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S86_LockStatementPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S86_LockStatement")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 43);
            }),

        new("S87", "BaseVirtualPatchAffectsDerived",
            "patch 基类虚方法, 派生类 base.Name() 调用基类实现, patch 后 base.Name() 返回带补丁的值, 派生类 override 串联体现补丁",
            "new S87_Derived().Name() == \"derived:base!\", new S87_BaseVirtual().Name() == \"base!\"",
            "ScenarioTargets/Scenarios/S87_BaseVirtualCall.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S87_BaseVirtualCallPatch.cs",
            a =>
            {
                var d = a.GetType("MonoModTestTargets.S87_Derived")!;
                var inst = Activator.CreateInstance(d)!;
                var got = (string)d.GetMethod("Name")!.Invoke(inst, null)!;
                var b = a.GetType("MonoModTestTargets.S87_BaseVirtual")!;
                var bi = Activator.CreateInstance(b)!;
                var bgot = (string)b.GetMethod("Name")!.Invoke(bi, null)!;
                var actual = $"derived={got}; base={bgot}";
                return new ScenarioResult(actual, got == "derived:base!" && bgot == "base!");
            }),

        new("S88", "EarlyReturnNoOrig",
            "完全替换方法体 (无 orig_), 负码短路返回 -1, 正码返回原逻辑+1",
            "Handle(-5)==-1, Handle(5)==11",
            "ScenarioTargets/Scenarios/S88_EarlyReturnNoOrig.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S88_EarlyReturnNoOrigPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S88_EarlyReturnNoOrig")!;
                var inst = Activator.CreateInstance(t)!;
                var neg = (int)t.GetMethod("Handle")!.Invoke(inst, new object[] { -5 })!;
                var pos = (int)t.GetMethod("Handle")!.Invoke(inst, new object[] { 5 })!;
                var actual = $"neg={neg}; pos={pos}";
                return new ScenarioResult(actual, neg == -1 && pos == 11);
            }),

        new("S89", "UsingDisposePatternWrap",
            "包装含 using (IDisposable) 语句的方法, orig_ 内 using 仍正确 Dispose, 补丁追加 !",
            "Run() == \"ran!\"",
            "ScenarioTargets/Scenarios/S89_UsingDisposePattern.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S89_UsingDisposePatternPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S89_UsingDisposePattern")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "ran!");
            }),

        new("S90", "DictionaryReturnWrap",
            "包装返回 Dictionary<string,int> 的方法, 调用 orig_ 后新增键 b=2",
            "Build() 含 a==1 且 b==2",
            "ScenarioTargets/Scenarios/S90_DictionaryReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S90_DictionaryReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S90_DictionaryReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (System.Collections.Generic.Dictionary<string, int>)t.GetMethod("Build")!.Invoke(inst, null)!;
                var actual = $"a={got["a"]}; hasB={got.ContainsKey("b")}";
                return new ScenarioResult(actual, got["a"] == 1 && got.ContainsKey("b") && got["b"] == 2);
            }),

        new("S91", "StructParamMethodWrap",
            "包装带自定义 struct 参数的方法 (与 S78 struct 返回互补, 这次是 struct 作为参数), 调用 orig_ 后加 10",
            "Read(handle{Value=5}) == 15",
            "ScenarioTargets/Scenarios/S91_RefStructParam.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S91_RefStructParamPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S91_StructParamMethod")!;
                var handleType = a.GetType("MonoModTestTargets.S91_Handle")!;
                var inst = Activator.CreateInstance(t)!;
                var h = Activator.CreateInstance(handleType)!;
                handleType.GetField("Value")!.SetValue(h, 5);
                var got = (int)t.GetMethod("Read")!.Invoke(inst, new object[] { h })!;
                return new ScenarioResult(got.ToString(), got == 15);
            }),

        new("S92", "StaticFieldInitInCctor",
            "patch 新增 static 字段并在静态构造函数 cctor 中初始化, Read() 读取它叠加到 orig_ 结果",
            "Read() == 7 (orig_ 返回 0 + Cache=7)",
            "ScenarioTargets/Scenarios/S92_StaticFieldCrossMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S92_StaticFieldCrossMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S92_StaticFieldCrossMethod")!;
                var got = (int)t.GetMethod("Read")!.Invoke(null, null)!;
                return new ScenarioResult(got.ToString(), got == 7);
            }),

        new("S93", "RefReadonlyParamWrap",
            "包装带 in (ref readonly) 双参数的方法, 调用 orig_ 后加 100",
            "Sum(2, 3) == 105",
            "ScenarioTargets/Scenarios/S93_RefReadonlyParam.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S93_RefReadonlyParamPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S93_RefReadonlyParam")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Sum")!.Invoke(inst, new object[] { 2, 3 })!;
                return new ScenarioResult(got.ToString(), got == 105);
            }),

        new("S94", "TwoDimArrayReturnWrap",
            "包装返回二维数组 int[,] 的方法, 调用 orig_ 后每个元素加 1",
            "Grid() == [[2,3],[4,5]]",
            "ScenarioTargets/Scenarios/S94_TwoDimArrayReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S94_TwoDimArrayReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S94_TwoDimArrayReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int[,])t.GetMethod("Grid")!.Invoke(inst, null)!;
                var actual = $"{got[0,0]},{got[0,1]},{got[1,0]},{got[1,1]}";
                return new ScenarioResult(actual, got[0,0]==2 && got[0,1]==3 && got[1,0]==4 && got[1,1]==5);
            }),

        new("S95", "AddGetterOnlyProperty",
            "patch 新增只读计算属性 (无 setter), 基于目标方法 Base() 计算",
            "Doubled == 2 且 Doubled 无 setter",
            "ScenarioTargets/Scenarios/S95_AddGetterOnlyProperty.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S95_AddGetterOnlyPropertyPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S95_AddGetterOnlyProperty")!;
                var inst = Activator.CreateInstance(t)!;
                var prop = t.GetProperty("Doubled")!;
                var got = (int)prop.GetValue(inst)!;
                var setter = prop.GetSetMethod(nonPublic: true);
                var actual = $"doubled={got}; setter={(setter is null ? "absent" : "present")}";
                return new ScenarioResult(actual, got == 2 && setter is null);
            }),

        new("S96", "StackallocLocalWrap",
            "包装含 stackalloc/Span 局部的方法, orig_ 内 stackalloc 仍正确, 补丁加 1",
            "Total(7) == 8",
            "ScenarioTargets/Scenarios/S96_StackallocLocal.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S96_StackallocLocalPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S96_StackallocLocal")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Total")!.Invoke(inst, new object[] { 7 })!;
                return new ScenarioResult(got.ToString(), got == 8);
            }),

        new("S97", "ForceCallvirtOnNonVirtual",
            "patch 方法施加 [MonoModForceCallvirt], 验证补丁后程序集可加载且方法行为正确 (调用约定强制为 callvirt 不破坏语义)",
            "Compute() == 15 (原值 10 + 5)",
            "ScenarioTargets/Scenarios/S97_ForceCallNonVirtual.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S97_ForceCallNonVirtualPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S97_ForceCallNonVirtual")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Compute")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 15);
            }),

        new("S98", "IfFlagConditionalInclude",
            "用 [MonoModIfFlag(\"s98_on\", true)] 条件 patch: harness 设置 s98_on=true 时补丁生效, Run() 追加 !",
            "Run() == \"orig!\"",
            "ScenarioTargets/Scenarios/S98_IfFlagInclude.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S98_IfFlagIncludePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S98_IfFlagInclude")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "orig!");
            }),

        new("S99", "IfFlagConditionalExclude",
            "用 [MonoModIfFlag(\"s99_on\", false)] 条件 patch: harness 未设置 s99_on, fallback=false, 补丁被跳过, Run() 保持原值",
            "Run() == \"orig\" (补丁未生效)",
            "ScenarioTargets/Scenarios/S99_IfFlagExclude.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S99_IfFlagExcludePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S99_IfFlagExclude")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "orig");
            }),

        new("S100", "ForceCallOnVirtualMethod",
            "patch override 虚方法并施加 [MonoModForceCall], 强制对 Compute 的调用用 call (非虚分派), 验证补丁后行为正确",
            "Compute() == 15 (原值 10 + 5)",
            "ScenarioTargets/Scenarios/S100_ForceCallVirtual.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S100_ForceCallVirtualPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S100_ForceCallVirtual")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Compute")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 15);
            }),

        new("S101", "LinkToReverseRelinkRegistration",
            "用 [MonoModLinkTo] 注册反向重链接 (将 Replacement 的调用重定向到 Source), 验证补丁流程不破坏且 Source 仍被 patch",
            "Source() == \"source!\" (LinkTo 注册不影响 Source patch)",
            "ScenarioTargets/Scenarios/S101_LinkToReverse.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S101_LinkToReversePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S101_LinkToReverse")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Source")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "source!");
            }),

        new("S102", "TargetModuleMatch",
            "用 [MonoModTargetModule(\"MonoModTestTargets\")] 条件 patch: 目标程序集名匹配, 补丁生效",
            "Run() == \"orig!\"",
            "ScenarioTargets/Scenarios/S102_TargetModuleMatch.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S102_TargetModuleMatchPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S102_TargetModuleMatch")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "orig!");
            }),

        new("S103", "TargetModuleExclude",
            "用 [MonoModTargetModule(\"SomeOtherAssembly\")] 条件 patch: 目标程序集名不匹配, 补丁被跳过, Run() 保持原值",
            "Run() == \"orig\" (补丁未生效)",
            "ScenarioTargets/Scenarios/S103_TargetModuleExclude.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S103_TargetModuleExcludePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S103_TargetModuleExclude")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "orig");
            }),

        new("S104", "OnPlatformAlwaysExcludesBug",
            "用 [MonoModOnPlatform(OSKind.Windows)] 条件 patch: 发现 MonoMod.Patcher 25.0.1 的 OnPlatform 逻辑 bug — 即使当前平台匹配, 非空平台列表也会被无条件排除 (MatchingConditionals 循环无 break, 循环后 status &= plats.Length==0 总使非空列表为 false). 因此补丁未生效, Run() 保持原值",
            "Run() == \"orig\" (补丁因 OnPlatform bug 未生效, 即使在 Windows 上)",
            "ScenarioTargets/Scenarios/S104_OnPlatformWindows.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S104_OnPlatformWindowsPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S104_OnPlatformWindows")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                var actual = $"run={got}; os={System.Environment.OSVersion.Platform}";
                return new ScenarioResult(actual, got == "orig");
            }),

        new("S105", "ForeachMethodWrap",
            "包装含 foreach 遍历的方法, orig_ 内 foreach 仍正确, 补丁加 1",
            "Sum([1,2,3]) == 7 (原 6 + 1)",
            "ScenarioTargets/Scenarios/S105_ForeachMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S105_ForeachMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S105_ForeachMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Sum")!.Invoke(inst, new object[] { new int[] { 1, 2, 3 } })!;
                return new ScenarioResult(got.ToString(), got == 7);
            }),

        new("S106", "CrossAssemblyDependencyStaging",
            "目标方法调用另一程序集 (MonoModHelperLib) 的类型; 补丁包装该方法, 验证跨程序集依赖被正确暂存到 staging 目录, MonoMod 解析器能找到依赖并完成补丁",
            "Compute(5) == 11 (HelperMath.Double(5)=10 + 1)",
            "ScenarioTargets/Scenarios/S106_CrossAssemblyDep.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S106_CrossAssemblyDepPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S106_CrossAssemblyDep")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Compute")!.Invoke(inst, new object[] { 5 })!;
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S107", "NullableRefParamWrap",
            "包装带 nullable 引用类型参数 (string?) 的方法, 在原返回后追加 !",
            "Greet(null)==\"hi anon!\", Greet(\"x\")==\"hi x!\"",
            "ScenarioTargets/Scenarios/S107_NullableRefParam.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S107_NullableRefParamPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S107_NullableRefParam")!;
                var inst = Activator.CreateInstance(t)!;
                var g0 = (string)t.GetMethod("Greet")!.Invoke(inst, new object?[] { null })!;
                var g1 = (string)t.GetMethod("Greet")!.Invoke(inst, new object[] { "x" })!;
                var actual = $"null={g0}; x={g1}";
                return new ScenarioResult(actual, g0 == "hi anon!" && g1 == "hi x!");
            }),

        new("S108", "SingletonPatternWrap",
            "包装单例类型的实例方法, 验证单例静态属性与实例方法 patch 共存",
            "S108_Singleton.Instance.Tag() == \"singleton!\"",
            "ScenarioTargets/Scenarios/S108_SingletonPattern.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S108_SingletonPatternPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S108_Singleton")!;
                var inst = t.GetProperty("Instance")!.GetValue(null)!;
                var got = (string)t.GetMethod("Tag")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "singleton!");
            }),

        new("S109", "ExplicitInterfaceAddPublic",
            "目标有显式接口实现 S109_IFoo.Bar, patch 新增公共 Bar 返回不同值, 验证显式接口路由不受影响",
            "公共 Bar()==\"public-bar\", 接口 ((S109_IFoo)obj).Bar()==\"bar\"",
            "ScenarioTargets/Scenarios/S109_ExplicitInterfaceMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S109_ExplicitInterfaceMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S109_ExplicitInterfaceMethod")!;
                var ifooType = a.GetType("MonoModTestTargets.S109_IFoo")!;
                var inst = Activator.CreateInstance(t)!;
                var pub = (string)t.GetMethod("Bar")!.Invoke(inst, null)!;
                var iface = (string)ifooType.GetMethod("Bar")!.Invoke(inst, null)!;
                var actual = $"public={pub}; iface={iface}";
                return new ScenarioResult(actual, pub == "public-bar" && iface == "bar");
            }),

        new("S110", "LinqMethodWrap",
            "包装使用 LINQ (Where+Sum) 的方法, orig_ 内 LINQ 仍正确, 补丁加 1",
            "SumEvens([1,2,3,4]) == 7 (偶数和 6 + 1)",
            "ScenarioTargets/Scenarios/S110_LinqMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S110_LinqMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S110_LinqMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("SumEvens")!.Invoke(inst, new object[] { new int[] { 1, 2, 3, 4 } })!;
                return new ScenarioResult(got.ToString(), got == 7);
            }),

        new("S111", "ByRefReturnFieldMutation",
            "包装 ref 返回方法, 通过 orig_ 拿到 ref 后修改底层私有字段 (与 S44 互补, 这次字段私有)",
            "Value() 后读 _v == 51 (原 1 + 50)",
            "ScenarioTargets/Scenarios/S111_ByRefPropertyField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S111_ByRefPropertyFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S111_ByRefPropertyField")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Value")!.Invoke(inst, null);
                var backing = t.GetField("_v", BindingFlags.NonPublic | BindingFlags.Instance)!;
                var got = (int)backing.GetValue(inst)!;
                return new ScenarioResult(got.ToString(), got == 51);
            }),

        new("S112", "GotoLabelWrap",
            "包装含 goto/label 控制流的方法, orig_ 内 goto 循环仍正确, 补丁加 1",
            "Loop(3) == 7 (1+2+3=6 + 1)",
            "ScenarioTargets/Scenarios/S112_GotoLabel.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S112_GotoLabelPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S112_GotoLabel")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Loop")!.Invoke(inst, new object[] { 3 })!;
                return new ScenarioResult(got.ToString(), got == 7);
            }),

        new("S113", "CtorBaseArgsPatch",
            "patch 派生类构造函数, 调用 orig_ctor (内部 base(tag)) 后追加 ! 到 Tag",
            "new S113_Derived(\"x\").Tag == \"x!\"",
            "ScenarioTargets/Scenarios/S113_CtorBaseArgs.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S113_CtorBaseArgsPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S113_Derived")!;
                var inst = Activator.CreateInstance(t, new object[] { "x" })!;
                var got = (string)t.GetField("Tag")!.GetValue(inst)!;
                return new ScenarioResult(got, got == "x!");
            }),

        new("S114", "ExtensionMethodPatch",
            "patch 静态扩展方法 Shout, 在原返回后追加 !",
            "\"hi\".Shout() == \"HI!\"",
            "ScenarioTargets/Scenarios/S114_ExtensionMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S114_ExtensionMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S114_Extensions")!;
                var got = (string)t.GetMethod("Shout")!.Invoke(null, new object[] { "hi" })!;
                return new ScenarioResult(got, got == "HI!");
            }),

        new("S115", "JaggedArrayReturnWrap",
            "包装返回交错数组 int[][] 的方法, 调用 orig_ 后追加子数组 [9]",
            "Build() 第三子数组 == [9]",
            "ScenarioTargets/Scenarios/S115_JaggedArrayReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S115_JaggedArrayReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S115_JaggedArrayReturn")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int[][])t.GetMethod("Build")!.Invoke(inst, null)!;
                var actual = $"len={got.Length}; last={string.Join(",", got[got.Length-1])}";
                return new ScenarioResult(actual, got.Length == 3 && got[2][0] == 9);
            }),

        new("S116", "SwitchExpressionWrap",
            "包装使用 switch 表达式的方法, 在原返回外加方括号",
            "Classify(0)==\"[zero]\", Classify(5)==\"[many]\"",
            "ScenarioTargets/Scenarios/S116_SwitchExpression.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S116_SwitchExpressionPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S116_SwitchExpression")!;
                var inst = Activator.CreateInstance(t)!;
                var c0 = (string)t.GetMethod("Classify")!.Invoke(inst, new object[] { 0 })!;
                var c5 = (string)t.GetMethod("Classify")!.Invoke(inst, new object[] { 5 })!;
                var actual = $"c0={c0}; c5={c5}";
                return new ScenarioResult(actual, c0 == "[zero]" && c5 == "[many]");
            }),

        new("S117", "CheckedContextWrap",
            "包装含 checked 上下文的方法, orig_ 内 checked 仍正确, 补丁加 1",
            "Mul(3, 4) == 13 (12 + 1)",
            "ScenarioTargets/Scenarios/S117_CheckedContext.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S117_CheckedContextPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S117_CheckedContext")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Mul")!.Invoke(inst, new object[] { 3, 4 })!;
                return new ScenarioResult(got.ToString(), got == 13);
            }),

        new("S118", "LocalFunctionWrap",
            "包装含局部函数的方法, orig_ 内局部函数仍正确, 补丁加 1",
            "Compute(5) == 11 (5*2=10 + 1)",
            "ScenarioTargets/Scenarios/S118_LocalFunction.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S118_LocalFunctionPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S118_LocalFunction")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Compute")!.Invoke(inst, new object[] { 5 })!;
                return new ScenarioResult(got.ToString(), got == 11);
            }),

        new("S119", "InitOnlyPropertyWrap",
            "patch 含 init-only 属性的类的方法, 在原返回后追加 !",
            "new S119_InitOnly().Greet() == \"hi anon!\"",
            "ScenarioTargets/Scenarios/S119_RecordType.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S119_RecordTypePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S119_InitOnly")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Greet")!.Invoke(inst, null)!;
                return new ScenarioResult(got, got == "hi anon!");
            }),

        new("S120", "DelegateFieldInvocationWrap",
            "包装调用委托字段的方法, orig_ 内委托字段调用仍正确, 补丁加 10",
            "Apply(5) == 16 (5+1=6 + 10)",
            "ScenarioTargets/Scenarios/S120_DelegateField.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S120_DelegateFieldPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S120_DelegateField")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Apply")!.Invoke(inst, new object[] { 5 })!;
                return new ScenarioResult(got.ToString(), got == 16);
            }),

        new("S121", "TryFinallyNoUsingWrap",
            "包装含 try/finally (无 using) 的方法, orig_ 内 finally 仍执行, 补丁追加 !",
            "Run() == \"ran!\" 且 CleanedUp==true",
            "ScenarioTargets/Scenarios/S121_PatternDispose.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S121_PatternDisposePatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S121_TryFinallyNoUsing")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Run")!.Invoke(inst, null)!;
                var cleaned = (bool)t.GetField("CleanedUp")!.GetValue(inst)!;
                var actual = $"run={got}; cleaned={cleaned}";
                return new ScenarioResult(actual, got == "ran!" && cleaned);
            }),

        new("S122", "NonGenericTaskWrap",
            "包装非泛型 async Task 方法, await orig_ 后设置完成标志",
            "DoAsync().Wait() 完成后 Completed==true",
            "ScenarioTargets/Scenarios/S122_NonGenericTaskReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S122_NonGenericTaskReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S122_NonGenericTaskReturn")!;
                var f = t.GetField("Completed", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (f is null) return new ScenarioResult("Completed field missing", false);
                f.SetValue(null, false);
                var inst = Activator.CreateInstance(t)!;
                var task = (System.Threading.Tasks.Task)t.GetMethod("DoAsync")!.Invoke(inst, null)!;
                task.Wait();
                var got = (bool)f.GetValue(null)!;
                return new ScenarioResult(got.ToString(), got);
            }),

        new("S123", "LazyFieldMethodWrap",
            "包装使用 Lazy<int> 字段的方法, orig_ 内 Lazy.Value 仍正确, 补丁加 1",
            "Get() == 43 (原 42 + 1)",
            "ScenarioTargets/Scenarios/S123_LazyFieldMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S123_LazyFieldMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S123_LazyFieldMethod")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (int)t.GetMethod("Get")!.Invoke(inst, null)!;
                return new ScenarioResult(got.ToString(), got == 43);
            }),

        new("S124", "NestedTernaryWrap",
            "包装含嵌套三元表达式的方法, 在原返回外加方括号",
            "Classify(0)==\"[zero]\", Classify(-1)==\"[neg]\", Classify(5)==\"[pos]\"",
            "ScenarioTargets/Scenarios/S124_NestedTernary.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S124_NestedTernaryPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S124_NestedTernary")!;
                var inst = Activator.CreateInstance(t)!;
                var c0 = (string)t.GetMethod("Classify")!.Invoke(inst, new object[] { 0 })!;
                var cn = (string)t.GetMethod("Classify")!.Invoke(inst, new object[] { -1 })!;
                var cp = (string)t.GetMethod("Classify")!.Invoke(inst, new object[] { 5 })!;
                var actual = $"0={c0}; -1={cn}; 5={cp}";
                return new ScenarioResult(actual, c0 == "[zero]" && cn == "[neg]" && cp == "[pos]");
            }),

        new("S125", "StringConcatMultiArgWrap",
            "包装使用 string.Concat 多参数的方法, 在原返回后追加 !",
            "Build(\"a\", 7, true) == \"a-7-True!\"",
            "ScenarioTargets/Scenarios/S125_StringConcatMultiArg.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S125_StringConcatMultiArgPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S125_StringConcatMultiArg")!;
                var inst = Activator.CreateInstance(t)!;
                var got = (string)t.GetMethod("Build")!.Invoke(inst, new object[] { "a", 7, true })!;
                return new ScenarioResult(got, got == "a-7-True!");
            }),

        new("S126", "AddNewEventAndFire",
            "patch 向目标类型新增 event 并新增 Fire() 方法触发它, 验证新增事件可订阅与触发",
            "订阅 handler(累加 5) 后 Fire() 一次 Hits == 5",
            "ScenarioTargets/Scenarios/S126_AddNewEvent.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S126_AddNewEventPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S126_AddNewEvent")!;
                var inst = Activator.CreateInstance(t)!;
                var ev = t.GetEvent("Done");
                if (ev is null) return new ScenarioResult("Done event missing", false);
                int captured = 0;
                System.EventHandler handler = (s, e) =>
                {
                    captured += 5;
                };
                ev.AddEventHandler(inst, handler);
                t.GetMethod("Fire")!.Invoke(inst, null);
                var actual = $"hits={captured}";
                return new ScenarioResult(actual, captured == 5);
            }),

        // --- Precise IL Insertion (MonoModRules + PostProcessor + Cecil) ---

        new("S200", "MiddleInsertVoidCall",
            "用 MonoModRules + PostProcessor 在 Run() 中 First() 与 Third() 之间插入 Second() (void 调用)",
            "Run() 后 Log == \"123\"",
            "ScenarioTargets/Scenarios/S200_MiddleInsertVoidCall.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S200_MiddleInsertVoidCallPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S200_MiddleInsertVoidCall")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "123");
            }),

        new("S201", "MiddleInsertAfterNonVoidReturn",
            "在 Process() 中 Compute() (返回 int 存入局部变量) 之后, Done() 之前插入 LogComputed()",
            "Process() 后 Recorded == 42, Value == 142",
            "ScenarioTargets/Scenarios/S201_MiddleInsertWithReturn.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S201_MiddleInsertWithReturnPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S201_MiddleInsertWithReturn")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Process")!.Invoke(inst, null);
                var recorded = (int)t.GetProperty("Recorded")!.GetValue(inst)!;
                var value = (int)t.GetProperty("Value")!.GetValue(inst)!;
                var actual = $"recorded={recorded}; value={value}";
                return new ScenarioResult(actual, recorded == 42 && value == 142);
            }),

        new("S202", "MiddleInsertWithMarker",
            "在 Step() 中 Begin() 与 End() 之间插入 Middle(), 验证标记方法 __PatchMarker 存在且方法上有 PatchInsertMarkerAttribute",
            "Step() 后 Log == \"BME\", __PatchMarker 方法存在, Step 方法有 PatchInsertMarkerAttribute",
            "ScenarioTargets/Scenarios/S202_MiddleInsertMarker.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S202_MiddleInsertMarkerPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S202_MiddleInsertMarker")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Step")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var markerType = a.GetType("MonoMod.PatchMarkers");
                var markerMethod = markerType?.GetMethod("__PatchMarker");
                var stepMethod = t.GetMethod("Step")!;
                var hasAttr = stepMethod.GetCustomAttributes(false)
                    .Any(attr => attr.GetType().Name == "PatchInsertMarkerAttribute");
                var actual = $"log={got}; marker={(markerMethod is null ? "absent" : "present")}; attr={(hasAttr ? "present" : "absent")}";
                return new ScenarioResult(actual, got == "BME" && markerMethod is not null && hasAttr);
            }),

        new("S203", "MiddleInsertInTryBlock",
            "在 SafeRun() 的 try 块中 A() 与 C() 之间插入 B(), 验证 try/finally EH 表未损坏",
            "SafeRun() 后 Log == \"ABC\"",
            "ScenarioTargets/Scenarios/S203_MiddleInsertInTry.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S203_MiddleInsertInTryPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S203_MiddleInsertInTry")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("SafeRun")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "ABC");
            }),

        // --- Advanced IL Insertion (complex anchor / stack / EH scenarios) ---

        new("S210", "LoopBodyInsert",
            "在 for 循环体内 Log.Append(i) 之前插入 Tick(), 每次 iteration 都执行插入的方法",
            "Run(3) 后 Log == \"ST0T1T2F\"",
            "ScenarioTargets/Scenarios/S210_LoopBodyInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S210_LoopBodyInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S210_LoopBodyInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, new object[] { 3 });
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "ST0T1T2F");
            }),

        new("S211", "InsertCallWithParameter",
            "在 Begin() 与 End() 之间插入 LogValue(Counter), 插入方法接受参数, 需加载属性值作为参数",
            "Run() 后 LoggedValue == 10, Counter == 15",
            "ScenarioTargets/Scenarios/S211_ParamInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S211_ParamInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S211_ParamInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var logged = (int)t.GetProperty("LoggedValue")!.GetValue(inst)!;
                var counter = (int)t.GetProperty("Counter")!.GetValue(inst)!;
                var actual = $"logged={logged}; counter={counter}";
                return new ScenarioResult(actual, logged == 10 && counter == 15);
            }),

        new("S212", "InsertAfterVirtualCall",
            "在虚方法 callvirt GetName() (返回值存入 stloc) 之后插入 PostProcess(), 验证虚方法调用锚点正确",
            "Build() 后 Log == \"[post]name\", 返回 \"name\"",
            "ScenarioTargets/Scenarios/S212_VirtualChainInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S212_VirtualChainInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S212_VirtualChainInsert")!;
                var inst = Activator.CreateInstance(t)!;
                var ret = (string)t.GetMethod("Build")!.Invoke(inst, null)!;
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var actual = $"log={log}; ret={ret}";
                return new ScenarioResult(actual, log == "[post]name" && ret == "name");
            }),

        new("S213", "MarkerOnlyStackNeutral",
            "在 GetPrefix() 返回值被直接消费 (string.Concat) 的场景中仅插入标记, 不扰动求值栈",
            "Build() 后 Log == \"pre--suf\", 返回 \"result\", 方法有 PatchInsertMarkerAttribute",
            "ScenarioTargets/Scenarios/S213_StackConsumeInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S213_StackConsumeInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S213_StackConsumeInsert")!;
                var inst = Activator.CreateInstance(t)!;
                var ret = (string)t.GetMethod("Build")!.Invoke(inst, null)!;
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var hasAttr = t.GetMethod("Build")!.GetCustomAttributes(false)
                    .Any(attr => attr.GetType().Name == "PatchInsertMarkerAttribute");
                var actual = $"log={log}; ret={ret}; attr={(hasAttr ? "present" : "absent")}";
                return new ScenarioResult(actual, log == "pre--suf" && ret == "result" && hasAttr);
            }),

        new("S214", "CatchBlockInsert",
            "在 catch 块内 Log.Append(\"caught\") 之前插入 HandleCatch(), 验证 catch EH 区域内插入正确",
            "SafeExec() 后 Log == \"handled-caught\"",
            "ScenarioTargets/Scenarios/S214_CatchBlockInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S214_CatchBlockInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S214_CatchBlockInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("SafeExec")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "handled-caught");
            }),

        new("S215", "StaticMethodInsert",
            "在静态方法 StepA() 与 StepC() 之间插入静态方法 StepB(), 不使用 ldarg_0, 用 call 而非 callvirt",
            "RunStatic() 后 SharedLog == \"ABC\"",
            "ScenarioTargets/Scenarios/S215_StaticInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S215_StaticInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S215_StaticInsert")!;
                // Reset static state before test
                var sb = (System.Text.StringBuilder)t.GetProperty("SharedLog")!.GetValue(null)!;
                sb.Clear();
                t.GetMethod("RunStatic")!.Invoke(null, null);
                var got = sb.ToString();
                return new ScenarioResult(got, got == "ABC");
            }),

        // --- Advanced IL Insertion (complex control flow / generics / multi-arg) ---

        new("S220", "GenericMethodCallInsert",
            "在 List<int>.Add(1) 与 Add(2) 之间插入 LogMid(), 锚点为泛型方法 callvirt List<int>::Add",
            "Populate() 后 Log == \"[mid]\", Items == [1,2,3]",
            "ScenarioTargets/Scenarios/S220_GenericMethodInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S220_GenericMethodInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S220_GenericMethodInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Populate")!.Invoke(inst, null);
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var items = (System.Collections.Generic.List<int>)t.GetProperty("Items")!.GetValue(inst)!;
                var actual = $"log={log}; items={string.Join(",", items)}";
                return new ScenarioResult(actual, log == "[mid]" && items.Count == 3 && items[0] == 1 && items[1] == 2 && items[2] == 3);
            }),

        new("S221", "NestedTryCatchInnerCatchInsert",
            "在嵌套 try/catch 的内层 catch 块中 Log.Append(\"inner-caught\") 之前插入 HandleInnerCatch()",
            "Run() 后 Log == \"[handled]inner-caught\"",
            "ScenarioTargets/Scenarios/S221_NestedTryCatchInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S221_NestedTryCatchInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S221_NestedTryCatchInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[handled]inner-caught");
            }),

        new("S222", "SwitchBranchInsert",
            "在 switch 的 case 2 分支中 CaseB() 调用后, ldstr \"two\" 之前插入 CaseBExtra()",
            "Classify(1)==\"one\" (Log有A), Classify(2)==\"two\" (Log有B[extra]), Classify(9)==\"other\" (Log有D)",
            "ScenarioTargets/Scenarios/S222_SwitchBranchInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S222_SwitchBranchInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S222_SwitchBranchInsert")!;
                var inst1 = Activator.CreateInstance(t)!;
                var r1 = (string)t.GetMethod("Classify")!.Invoke(inst1, new object[] { 1 })!;
                var log1 = (string)t.GetProperty("Log")!.GetValue(inst1)!.ToString()!;

                var inst2 = Activator.CreateInstance(t)!;
                var r2 = (string)t.GetMethod("Classify")!.Invoke(inst2, new object[] { 2 })!;
                var log2 = (string)t.GetProperty("Log")!.GetValue(inst2)!.ToString()!;

                var inst3 = Activator.CreateInstance(t)!;
                var r3 = (string)t.GetMethod("Classify")!.Invoke(inst3, new object[] { 9 })!;
                var log3 = (string)t.GetProperty("Log")!.GetValue(inst3)!.ToString()!;

                var actual = $"r1={r1}/{log1}; r2={r2}/{log2}; r3={r3}/{log3}";
                return new ScenarioResult(actual,
                    r1 == "one" && log1 == "A" &&
                    r2 == "two" && log2 == "B[extra]" &&
                    r3 == "other" && log3 == "D");
            }),

        new("S223", "RefParamMethodInsert",
            "在两次 Bump(ref x, delta) 调用之间插入 LogMid(), 验证 ref 参数方法的锚点匹配正确",
            "Process() 后 Log == \"bump:10;[mid]bump:30;\", Total == 30",
            "ScenarioTargets/Scenarios/S223_RefParamInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S223_RefParamInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S223_RefParamInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Process")!.Invoke(inst, null);
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var total = (int)t.GetProperty("Total")!.GetValue(inst)!;
                var actual = $"log={log}; total={total}";
                return new ScenarioResult(actual, log == "bump:10;[mid]bump:30;" && total == 30);
            }),

        new("S224", "MultiReturnBranchInsert",
            "在多返回点方法的 early-return 分支中 MarkEarly() 后插入 AfterMarkEarly(), 验证只在负数分支插入",
            "Evaluate(-1)==\"neg\" (Log有early[after-early]), Evaluate(5)==\"non-neg\" (Log有late, 无after-early)",
            "ScenarioTargets/Scenarios/S224_MultiReturnInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S224_MultiReturnInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S224_MultiReturnInsert")!;
                var inst1 = Activator.CreateInstance(t)!;
                var r1 = (string)t.GetMethod("Evaluate")!.Invoke(inst1, new object[] { -1 })!;
                var log1 = (string)t.GetProperty("Log")!.GetValue(inst1)!.ToString()!;

                var inst2 = Activator.CreateInstance(t)!;
                var r2 = (string)t.GetMethod("Evaluate")!.Invoke(inst2, new object[] { 5 })!;
                var log2 = (string)t.GetProperty("Log")!.GetValue(inst2)!.ToString()!;

                var actual = $"r1={r1}/{log1}; r2={r2}/{log2}";
                return new ScenarioResult(actual,
                    r1 == "neg" && log1 == "early;[after-early]" &&
                    r2 == "non-neg" && log2 == "late;");
            }),

        new("S225", "MultiParameterInsert",
            "在 Start() 与 End() 之间插入 LogDimensions(Width, Height), 插入方法带两个参数, 需加载两个属性值",
            "Run() 后 Log == \"[100x200]\"",
            "ScenarioTargets/Scenarios/S225_MultiParamInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S225_MultiParamInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S225_MultiParamInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[100x200]");
            }),

        // --- S226-S231: try-finally / multi-insert / chain / lock / using / before-ret ---

        new("S226", "TryFinallyBlockInsert",
            "在 try-finally 的 finally 块中 Cleanup() 之前插入 MarkFinally(), 验证 finally EH 区域内插入正确",
            "Run() 后 Log == \"work;[finally];\", CleanupDone == true",
            "ScenarioTargets/Scenarios/S226_TryFinallyInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S226_TryFinallyInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S226_TryFinallyInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var cleanup = (bool)t.GetProperty("CleanupDone")!.GetValue(inst)!;
                var actual = $"log={log}; cleanup={cleanup}";
                return new ScenarioResult(actual, log == "work;[finally];" && cleanup);
            }),

        new("S227", "MultiInsertSameMethod",
            "在同一个方法 Run() 中做两次插入: Alpha() 后插入 AfterAlpha(), Beta() 后插入 AfterBeta()",
            "Run() 后 Log == \"A;[a];B;[b];G;\"",
            "ScenarioTargets/Scenarios/S227_MultiInsertSameMethod.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S227_MultiInsertSameMethodPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S227_MultiInsertSameMethod")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "A;[a];B;[b];G;");
            }),

        new("S228", "ChainedCallInsert",
            "在 Self().Done() 链式调用完成后插入 PostChain(), 验证链式调用的栈状态正确处理",
            "Run() 后 Log == \"self;done;[post];\"",
            "ScenarioTargets/Scenarios/S228_ChainedCallInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S228_ChainedCallInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S228_ChainedCallInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "self;done;[post];");
            }),

        new("S229", "LockBodyInsert",
            "在 lock 语句体内 Locked() 之前插入 PreLocked(), 验证 lock 的 Monitor EH 区域内插入正确",
            "Run() 后 Log == \"[pre];locked;finish;\"",
            "ScenarioTargets/Scenarios/S229_LockBodyInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S229_LockBodyInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S229_LockBodyInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[pre];locked;finish;");
            }),

        new("S230", "UsingBodyInsert",
            "在 using 语句体内 Inner() 之前插入 PreInner(), 验证 using 的 Dispose EH 区域内插入正确",
            "Run() 后 Log == \"[pre];inner;after;\", Disposed == true",
            "ScenarioTargets/Scenarios/S230_UsingBodyInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S230_UsingBodyInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S230_UsingBodyInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var log = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                var disposed = (bool)t.GetProperty("Disposed")!.GetValue(inst)!;
                var actual = $"log={log}; disposed={disposed}";
                return new ScenarioResult(actual, log == "[pre];inner;after;" && disposed);
            }),

        new("S231", "BeforeRetInsert",
            "在 First() 之后, ret 指令之前插入 BeforeReturn(), 验证方法末尾插入正确",
            "Run() 后 Log == \"1;[before-ret];\"",
            "ScenarioTargets/Scenarios/S231_BeforeRetInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S231_BeforeRetInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S231_BeforeRetInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[before-ret];");
            }),

        // --- S232-S237: cross-type / enum / loops / boxed / string-const ---

        new("S232", "CrossTypeMethodInsert",
            "在 Begin() 与 End() 之间插入 CrossNote(), 该方法内部调用另一类型 S232_CrossTypeHelper 的静态方法",
            "Run() 后 S232_CrossTypeHelper.SharedLog == \"begin;[mid];end;\"",
            "ScenarioTargets/Scenarios/S232_CrossTypeInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S232_CrossTypeInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S232_CrossTypeInsert")!;
                var helper = a.GetType("MonoModTestTargets.S232_CrossTypeHelper")!;
                var sb = (System.Text.StringBuilder)helper.GetProperty("SharedLog")!.GetValue(null)!;
                sb.Clear();
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = sb.ToString();
                return new ScenarioResult(got, got == "begin;[mid];end;");
            }),

        new("S233", "EnumArgumentInsert",
            "在 Start() 与 Stop() 之间插入 LogLevel(Current), 参数为枚举类型, 从属性加载",
            "Run() 后 Log == \"[level:Medium];\"",
            "ScenarioTargets/Scenarios/S233_EnumArgInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S233_EnumArgInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S233_EnumArgInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[level:Medium];");
            }),

        new("S234", "DoWhileLoopInsert",
            "在 do-while 循环体内 Tick() 之前插入 PreTick(), 每次迭代都执行",
            "Run(3) 后 Log == \"[pre];T[pre];T[pre];TC\"",
            "ScenarioTargets/Scenarios/S234_DoWhileInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S234_DoWhileInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S234_DoWhileInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, new object[] { 3 });
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[pre];T[pre];T[pre];TC");
            }),

        new("S235", "WhileLoopInsert",
            "在 while 循环体内 Step() 之前插入 PreStep(), 每次迭代都执行",
            "Run(2) 后 Log == \"[pre];S[pre];SD\"",
            "ScenarioTargets/Scenarios/S235_WhileInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S235_WhileInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S235_WhileInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, new object[] { 2 });
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[pre];S[pre];SD");
            }),

        new("S236", "BoxedValueArgInsert",
            "在 First() 与 Last() 之间插入 LogBoxed(BoxedValue), int 属性值需 box 为 object 后传入",
            "Run() 后 Log == \"1;[boxed:77];last;\"",
            "ScenarioTargets/Scenarios/S236_BoxedValueInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S236_BoxedValueInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S236_BoxedValueInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[boxed:77];last;");
            }),

        new("S237", "StringConstArgInsert",
            "在 Alpha() 与 Omega() 之间插入 LogTag(\"mid\"), 参数为字符串常量, 用 ldstr 加载",
            "Run() 后 Log == \"A;[mid];O;\"",
            "ScenarioTargets/Scenarios/S237_StringArgInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S237_StringArgInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S237_StringArgInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "A;[mid];O;");
            }),

        // --- S238-S243: local func / switch expr / ternary / checked / goto / params ---

        new("S238", "LocalFunctionInsert",
            "在含局部函数的方法中 Before() 后插入 MidNote(), 验证局部函数的 IL 不受影响",
            "Run() 后 Log == \"B;[mid];r=25;A;\"",
            "ScenarioTargets/Scenarios/S238_LocalFuncInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S238_LocalFuncInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S238_LocalFuncInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "B;[mid];r=25;A;");
            }),

        new("S239", "SwitchExpressionInsert",
            "在含 switch 表达式的方法中 Start() 后插入 MidNote(), 验证 switch 表达式 IL 不受影响",
            "Run() 后 Log == \"S;[mid];label=many;E;\"",
            "ScenarioTargets/Scenarios/S239_SwitchExprInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S239_SwitchExprInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S239_SwitchExprInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "S;[mid];label=many;E;");
            }),

        new("S240", "TernaryExpressionInsert",
            "在三元表达式结果存入局部变量后, 第二次 Log.Append 之前插入 MidNote(), 验证三元分支栈正确",
            "Run(true) 后 Log == \"start;[mid];delta=1;\", Run(false) 后 Log == \"start;[mid];delta=-1;\"",
            "ScenarioTargets/Scenarios/S240_TernaryInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S240_TernaryInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S240_TernaryInsert")!;
                var inst1 = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst1, new object[] { true });
                var log1 = (string)t.GetProperty("Log")!.GetValue(inst1)!.ToString()!;

                var inst2 = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst2, new object[] { false });
                var log2 = (string)t.GetProperty("Log")!.GetValue(inst2)!.ToString()!;

                var actual = $"true={log1}; false={log2}";
                return new ScenarioResult(actual, log1 == "start;[mid];delta=1;" && log2 == "start;[mid];delta=-1;");
            }),

        new("S241", "CheckedContextInsert",
            "在 checked 块内 First() 后插入 MidNote(), 验证 checked 算术上下文不受影响",
            "Run() 后 Log == \"1;[mid];sum=300;last;\"",
            "ScenarioTargets/Scenarios/S241_CheckedInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S241_CheckedInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S241_CheckedInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[mid];sum=300;last;");
            }),

        new("S242", "GotoFlowInsert",
            "在 goto 控制流方法中 Enter() 后插入 MidNote(), 验证 goto/label 跳转目标未损坏",
            "Run(3) 后 Log == \"enter;[mid];012exit;\"",
            "ScenarioTargets/Scenarios/S242_GotoFlowInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S242_GotoFlowInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S242_GotoFlowInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, new object[] { 3 });
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "enter;[mid];012exit;");
            }),

        new("S243", "ParamsArrayInsert",
            "在含 params 数组方法的方法中 First() 后插入 MidNote(), 验证 params 调用不受影响",
            "Run() 后 Log == \"1;[mid];r=a-b-c;last;\"",
            "ScenarioTargets/Scenarios/S243_ParamsArrayInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S243_ParamsArrayInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S243_ParamsArrayInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[mid];r=a-b-c;last;");
            }),

        // --- S244-S250: nullable / ref struct / exception filter / nested try / recursive / static / index ---

        new("S244", "NullableReturnInsert",
            "在含 nullable 返回值的方法中 First() 后插入 MidNote(), 验证 nullable 调用不受影响",
            "Run() 后 Log == \"1;[mid];name=name;last;\"",
            "ScenarioTargets/Scenarios/S244_NullableReturnInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S244_NullableReturnInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S244_NullableReturnInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[mid];name=name;last;");
            }),

        new("S245", "RefStructParamInsert",
            "在含 ref struct (Span<int>) 参数方法的方法中 First() 后插入 MidNote(), 验证 Span 调用不受影响",
            "Run() 后 Log == \"1;[mid];sum=6;last;\"",
            "ScenarioTargets/Scenarios/S245_RefStructInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S245_RefStructInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S245_RefStructInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[mid];sum=6;last;");
            }),

        new("S246", "ExceptionFilterInsert",
            "在带 when 过滤器的 catch 块中 HandleError() 之前插入 PreHandle(), 验证 filter EH 区域内插入正确",
            "SafeExec() 后 Log == \"[pre];handled;\"",
            "ScenarioTargets/Scenarios/S246_ExceptionFilterInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S246_ExceptionFilterInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S246_ExceptionFilterInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("SafeExec")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "[pre];handled;");
            }),

        new("S247", "NestedTryInTryInsert",
            "在外层 try 中 StepA() 后, 内层 try 之前插入 MidNote(), 验证嵌套 try EH 区域正确",
            "Run() 后 Log == \"A;[mid];B;\"",
            "ScenarioTargets/Scenarios/S247_NestedTryInTryInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S247_NestedTryInTryInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S247_NestedTryInTryInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "A;[mid];B;");
            }),

        new("S248", "RecursiveMethodInsert",
            "在递归方法中, 递归调用 Factorial() 之前插入 PreRecurse(), 验证递归调用锚点正确",
            "Factorial(3) 后 Log == \"f3;[pre];f2;[pre];f1;[pre];base;\"",
            "ScenarioTargets/Scenarios/S248_RecursiveInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S248_RecursiveInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S248_RecursiveInsert")!;
                var inst = Activator.CreateInstance(t)!;
                var sb = (System.Text.StringBuilder)t.GetProperty("Log")!.GetValue(inst)!;
                sb.Clear();
                var result = (int)t.GetMethod("Factorial")!.Invoke(inst, new object[] { 3 })!;
                var got = sb.ToString();
                var actual = $"result={result}; log={got}";
                return new ScenarioResult(actual, result == 6 && got == "f3;[pre];f2;[pre];base;");
            }),

        new("S249", "StaticMethodContextInsert",
            "在静态方法 Init() 中 Append(\"init;\") 之后, set_Tag 之前插入静态 MidNote(), 验证静态方法间插入正确",
            "Init() 后 Log == \"init;[mid];\", Tag == \"ready\"",
            "ScenarioTargets/Scenarios/S249_StaticCtorContext.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S249_StaticCtorContextPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S249_StaticCtorContext")!;
                var sb = (System.Text.StringBuilder)t.GetProperty("Log")!.GetValue(null)!;
                sb.Clear();
                var tagProp = t.GetProperty("Tag")!;
                tagProp.SetValue(null, "");
                t.GetMethod("Init")!.Invoke(null, null);
                var log = sb.ToString();
                var tag = (string)tagProp.GetValue(null)!;
                var actual = $"log={log}; tag={tag}";
                return new ScenarioResult(actual, log == "init;[mid];" && tag == "ready");
            }),

        new("S250", "IndexAccessInsert",
            "在含数组索引访问的方法中 First() 后插入 MidNote(), 验证索引访问 IL 不受影响",
            "Run() 后 Log == \"1;[mid];val=20;last;\"",
            "ScenarioTargets/Scenarios/S250_IndexAccessInsert.cs",
            "ScenarioTargets.Patches.mm/Scenarios/S250_IndexAccessInsertPatch.cs",
            a =>
            {
                var t = a.GetType("MonoModTestTargets.S250_IndexAccessInsert")!;
                var inst = Activator.CreateInstance(t)!;
                t.GetMethod("Run")!.Invoke(inst, null);
                var got = (string)t.GetProperty("Log")!.GetValue(inst)!.ToString()!;
                return new ScenarioResult(got, got == "1;[mid];val=20;last;");
            }),
    };

    private static string ReadFile(string relative) =>
        File.ReadAllText(Path.Combine(s_testsRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindTestsRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "ScenarioTargets")) &&
                Directory.Exists(Path.Combine(dir.FullName, "ScenarioTargets.Patches.mm")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find tests root.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}