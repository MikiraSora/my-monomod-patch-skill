# MonoMod Patch DLL Skill 验证记录

- 目标程序集: MonoModTestTargets.dll
- 补丁程序集: MonoModTestTargets.Patches.mm.dll
- 打补丁后: MonoModTestTargets_modded.dll
- MonoMod.Patcher: 25.0.1
- 说明: 一个目标程序集包含多个场景类, 一个 .mm.dll 补丁程序集包含对应多个 patch_ 类型, 一次性打补丁后逐场景反射验证 (MonoMod 多类型同程序集补丁的标准用法)

## S01 WrapInstanceMethod

**需求**: 在调用原方法基础上, 把返回值改成 [P] + 原结果大写

**期望**: Greet("alice") == "[P] HI ALICE"

**实际**: [P] HI ALICE

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S01_WrapInstanceMethod
{
    public string Greet(string name) => "hi " + name;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S01_WrapInstanceMethod : S01_WrapInstanceMethod
{
    public extern string orig_Greet(string name);

    public string Greet(string name) => "[P] " + orig_Greet(name).ToUpperInvariant();
}
```

## S02 ReplaceInstanceMethod

**需求**: 完全替换方法体, 不调用原方法, 返回 x+100

**期望**: Score(5) == 105

**实际**: 105

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S02_ReplaceInstanceMethod
{
    public int Score(int x) => x * 2;
}
```

### Patch 代码
```csharp
namespace MonoModTestTargets;

internal class patch_S02_ReplaceInstanceMethod : S02_ReplaceInstanceMethod
{
    public int Score(int x) => x + 100;
}
```

## S03 WrapStaticMethod

**需求**: 包装静态方法, 在原返回后追加 !

**期望**: Echo("hi") == "hi!"

**实际**: hi!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S03_WrapStaticMethod
{
    public static string Echo(string s) => s;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S03_WrapStaticMethod : S03_WrapStaticMethod
{
    public extern static string orig_Echo(string s);

    public static string Echo(string s) => orig_Echo(s) + "!";
}
```

## S04 PatchInstanceConstructor

**需求**: 用 [MonoModConstructor] patch 实例构造函数, 调用 orig_ctor 后改写 Marker

**期望**: Marker == "ctor:patched"

**实际**: ctor:patched

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S04_PatchInstanceConstructor
{
    public string Marker { get; set; } = "unset";

    public S04_PatchInstanceConstructor()
    {
        Marker = "ctor:orig";
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S04_PatchInstanceConstructor : S04_PatchInstanceConstructor
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        Marker = "ctor:patched";
    }
}
```

## S05 PatchStaticConstructor

**需求**: 用 [MonoModConstructor] patch 静态构造函数, 调用 orig_cctor 后改写 StaticMarker

**期望**: StaticMarker == "sctor:patched"

**实际**: sctor:patched

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S05_PatchStaticConstructor
{
    public static string StaticMarker { get; set; } = "unset";

    static S05_PatchStaticConstructor()
    {
        StaticMarker = "sctor:orig";
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S05_PatchStaticConstructor : S05_PatchStaticConstructor
{
    public extern static void orig_cctor();

    [MonoModConstructor]
    public static void cctor()
    {
        orig_cctor();
        StaticMarker = "sctor:patched";
    }
}
```

## S06 AddNewMembers

**需求**: 向已 patch 的类型新增字段/属性/方法, 且原方法保持不变

**期望**: ExtraField=="extra", ExtraProp=="prop", ExtraMethod()=="extra-method", Base()=="base"

**实际**: field=extra; prop=prop; method=extra-method; base=base

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S06_AddNewMembers
{
    public string Base() => "base";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S06_AddNewMembers : S06_AddNewMembers
{
    public string ExtraField;
    public string ExtraProp { get; set; }

    public string ExtraMethod() => "extra-method";

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

## S07 IgnoreHelperNotCalled

**需求**: [MonoModIgnore] 标记的辅助方法不应被复制进目标程序集, 且主方法仍被 patch

**期望**: Run()=="run+patched" 且 Helper 方法不存在于 modded 类型

**实际**: run=run+patched; helper=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S07_IgnoreHelper
{
    public string Run() => "run";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S07_IgnoreHelper : S07_IgnoreHelper
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+patched";

    [MonoModIgnore]
    private static string Helper() => "ignored";
}
```

## S08 ExplicitPatchAttribute

**需求**: patch 类位于不同命名空间, 用 [MonoModPatch("global::...")] 显式指定目标类型

**期望**: Label() == "label!"

**实际**: label!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S08_ExplicitTarget
{
    public string Label() => "label";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets.Patches;

[MonoModPatch("global::MonoModTestTargets.S08_ExplicitTarget")]
internal class S08_ExplicitPatch : MonoModTestTargets.S08_ExplicitTarget
{
    public extern string orig_Label();

    public string Label() => orig_Label() + "!";
}
```

## S09 RefParameter

**需求**: 包装带 ref 参数的方法, 先调用 orig_ 再额外加 10

**期望**: Bump(ref 5) 后 x == 16

**实际**: 16

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S09_RefParameter
{
    public void Bump(ref int x)
    {
        x += 1;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S09_RefParameter : S09_RefParameter
{
    public extern void orig_Bump(ref int x);

    public void Bump(ref int x)
    {
        orig_Bump(ref x);
        x += 10;
    }
}
```

## S10 OutParameter

**需求**: 包装带 out 参数的方法, 调用 orig_ 后把 out 值加 100

**期望**: TryGet(out r) 返回 true 且 r == 101

**实际**: ret=True; r=101

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S10_OutParameter
{
    public bool TryGet(out int r)
    {
        r = 1;
        return true;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S10_OutParameter : S10_OutParameter
{
    public extern bool orig_TryGet(out int r);

    public bool TryGet(out int r)
    {
        bool ok = orig_TryGet(out r);
        r += 100;
        return ok;
    }
}
```

## S11 PropertyGetterSetter

**需求**: 分别 patch 属性的 getter 和 setter, getter 在原值后追加 :get, setter 存入值+:set

**期望**: set "x" 后 get == "x:set:get"

**实际**: x:set:get

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S11_PropertyAccessors
{
    public string Value { get; set; } = "v";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S11_PropertyAccessors : S11_PropertyAccessors
{
    public extern string orig_get_Value();
    public extern void orig_set_Value(string value);

    public string Value
    {
        get => orig_get_Value() + ":get";
        set => orig_set_Value(value + ":set");
    }
}
```

## S12 MethodOverloads

**需求**: 只 patch 重载中的一个 Do(int), 另一个 Do(string) 保持不变

**期望**: Do(5)=="int:5!", Do("z")=="str:z"

**实际**: int=int:5!; str=str:z

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S12_MethodOverloads
{
    public string Do(int x) => "int:" + x;

    public string Do(string s) => "str:" + s;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S12_MethodOverloads : S12_MethodOverloads
{
    public extern string orig_Do(int x);

    public string Do(int x) => orig_Do(x) + "!";
}
```

## S13 PrivateMethodPatch

**需求**: patch 私有方法 Secret, 通过公共方法 Reveal 间接验证行为已改变

**期望**: Reveal() == "secret!"

**实际**: secret!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S13_PrivateMethod
{
    public string Reveal() => Secret();

    private string Secret() => "secret";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S13_PrivateMethod : S13_PrivateMethod
{
    public extern string orig_Secret();

    public string Secret() => orig_Secret() + "!";
}
```

## S14 ReplaceModifier

**需求**: 用 [MonoModReplace] 完全替换方法体且不生成 orig_ 副本

**期望**: Mode()=="fast" 且类型上不存在 orig_Mode 方法

**实际**: mode=fast; origMode=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S14_ReplaceModifier
{
    public string Mode() => "normal";
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S14_ReplaceModifier : S14_ReplaceModifier
{
    [MonoModReplace]
    public string Mode() => "fast";
}
```

## S15 GenericMethod

**需求**: 包装泛型方法, 在原返回外层加方括号

**期望**: Format<int>(7) == "[fmt:7]"

**实际**: [fmt:7]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S15_GenericMethod
{
    public string Format<T>(T v) => "fmt:" + v;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S15_GenericMethod : S15_GenericMethod
{
    public extern string orig_Format<T>(T v);

    public string Format<T>(T v) => "[" + orig_Format<T>(v) + "]";
}
```

## S16 NestedType

**需求**: patch 嵌套类型, 在原返回后追加 !

**期望**: S16_NestedOwner.Inner.Id() == "inner!"

**实际**: inner!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S16_NestedOwner
{
    public class Inner
    {
        public string Id() => "inner";
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S16_NestedOwner : S16_NestedOwner
{
    internal class patch_Inner : S16_NestedOwner.Inner
    {
        public extern string orig_Id();

        public string Id() => orig_Id() + "!";
    }
}
```

## S17 ExceptionSwallow

**需求**: 包装会抛异常的方法, 捕获后返回安全值

**期望**: Risky() == "safe" (原方法抛 InvalidOperationException)

**实际**: safe

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S17_ExceptionSource
{
    public string Risky() => throw new InvalidOperationException("boom");
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S17_ExceptionSource : S17_ExceptionSource
{
    public extern string orig_Risky();

    public string Risky()
    {
        try
        {
            return orig_Risky();
        }
        catch (InvalidOperationException)
        {
            return "safe";
        }
    }
}
```

## S18 ParamsArray

**需求**: 包装 params string[] 方法, 在原返回后追加 !

**期望**: Join("a","b") == "a,b!"

**实际**: a,b!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S18_ParamsArray
{
    public string Join(params string[] parts) => string.Join(",", parts);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S18_ParamsArray : S18_ParamsArray
{
    public extern string orig_Join(string[] parts);

    public string Join(string[] parts) => orig_Join(parts) + "!";
}
```

## S20 InheritedMethodPatch

**需求**: patch 基类方法, 派生类继承的调用也应体现补丁行为

**期望**: new S20_Derived().Who() == "base!"

**实际**: base!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S20_Base
{
    public string Who() => "base";
}

public class S20_Derived : S20_Base
{
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S20_Base : S20_Base
{
    public extern string orig_Who();

    public string Who() => orig_Who() + "!";
}
```

## S21 OriginalNameAttribute

**需求**: 用 [MonoModOriginal] + [MonoModOriginalName] 自定义原方法名, 包装原方法并追加 !

**期望**: Code() == "c!"

**实际**: c!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S21_OriginalName
{
    public string Code() => "c";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S21_OriginalName : S21_OriginalName
{
    [MonoModOriginal]
    public extern string original_Code();

    [MonoModOriginalName("original_Code")]
    public string Code() => original_Code() + "!";
}
```

## S22 RemoveMember

**需求**: 用 [MonoModRemove] 把 Extra() 方法从目标类型移除, Keep() 保持不变

**期望**: Extra 方法不存在于 modded 类型, Keep()=="keep"

**实际**: keep=keep; extra=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S22_RemoveMember
{
    public string Keep() => "keep";

    public string Extra() => "extra";
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S22_RemoveMember : S22_RemoveMember
{
    // Remove Extra() entirely from the patched type; Keep() stays untouched.
    [MonoModRemove]
    public string Extra() => "removed";
}
```

## S23 MonoModPublicMember

**需求**: 用 [MonoModPublic] 把目标 internal 方法在补丁后变为 public, 并包装返回值追加 !

**期望**: Hidden()=="hidden!" 且 Hidden 在 modded 类型上为 public

**实际**: hidden=hidden!; isPublic=True

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S23_MonoModPublic
{
    internal string Hidden() => "hidden";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S23_MonoModPublic : S23_MonoModPublic
{
    public extern string orig_Hidden();

    [MonoModPublic]
    public string Hidden() => orig_Hidden() + "!";
}
```

## S24 NoNewSkipsAbsentMethod

**需求**: [MonoModNoNew] 标记的方法在目标中不存在时应被跳过, 主方法仍被 patch

**期望**: Exists()=="yes!" 且 NotInTarget 不存在于 modded 类型

**实际**: exists=yes!; notInTarget=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S24_NoNew
{
    public string Exists() => "yes";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S24_NoNew : S24_NoNew
{
    public extern string orig_Exists();

    public string Exists() => orig_Exists() + "!";

    [MonoModNoNew]
    public string NotInTarget() => "should-not-be-added";
}
```

## S25 VirtualOverridePatch

**需求**: patch 派生类的 override 方法, 仅派生类调用体现补丁, 基类调用不变

**期望**: new S25_Derived().Virt()=="derived!", new S25_Base().Virt()=="base-virt"

**实际**: derived=derived!; base=base-virt

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S25_Base
{
    public virtual string Virt() => "base-virt";
}

public class S25_Derived : S25_Base
{
    public override string Virt() => "derived";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S25_Derived : S25_Derived
{
    public extern string orig_Virt();

    public override string Virt() => orig_Virt() + "!";
}
```

## S26 VoidSideEffectWrap

**需求**: 包装 void 方法, 调用 orig_ 后额外累加 10, 验证原方法仅执行一次

**期望**: 重置 Count 后 Tick() 一次 Count == 11

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S26_VoidSideEffect
{
    public static int Count;

    public void Tick()
    {
        Count += 1;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S26_VoidSideEffect : S26_VoidSideEffect
{
    public extern void orig_Tick();

    public void Tick()
    {
        orig_Tick();
        Count += 10;
    }
}
```

## S28 GenericTypePatch

**需求**: patch 泛型类型 S28_Box<T> 的方法, 在原返回后追加 !

**期望**: new S28_Box<int>().Show() == "Int32!"

**实际**: Int32!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S28_Box<T>
{
    public string Show() => typeof(T).Name;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S28_Box<T> : S28_Box<T>
{
    public extern string orig_Show();

    public string Show() => orig_Show() + "!";
}
```

## S30 OptionalParameter

**需求**: 包装带默认参数的方法, 默认值语义保持, 返回值追加 !

**期望**: Greet("hi") == "hi.!"

**实际**: hi.!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S30_OptionalParameter
{
    public string Greet(string name, string punc = ".") => name + punc;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S30_OptionalParameter : S30_OptionalParameter
{
    public extern string orig_Greet(string name, string punc);

    public string Greet(string name, string punc = ".") => orig_Greet(name, punc) + "!";
}
```

## S31 InParameter

**需求**: 包装带 in 参数的方法, 调用 orig_ 后再加 10

**期望**: Add(5) == 16

**实际**: 16

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S31_InParameter
{
    public int Add(in int x) => x + 1;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S31_InParameter : S31_InParameter
{
    public extern int orig_Add(in int x);

    public int Add(in int x) => orig_Add(in x) + 10;
}
```

## S34 MultiplePatchesSameType

**需求**: 两个不同 patch_ 类型分别 patch 同一目标类型的 A 和 B 方法, 一次性补丁应同时生效

**期望**: A()=="a!A", B()=="b!B"

**实际**: a=a!A; b=b!B

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S34_Multi
{
    public string A() => "a";

    public string B() => "b";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S34_Multi")]
internal class S34_Multi_PatchA : S34_Multi
{
    public extern string orig_A();

    public string A() => orig_A() + "!A";
}
```

## S35 StructMethodPatch

**需求**: patch 值类型(struct)的方法, 用 [MonoModPatch] 显式指定目标类型(因 struct 无法继承), 在原返回上加 1

**期望**: new S35_Point{X=5}.Twice() == 11

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public struct S35_Point
{
    public int X;

    public int Twice() => X * 2;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S35_Point")]
internal class patch_S35_Point
{
    public int X;

    public extern int orig_Twice();

    public int Twice() => orig_Twice() + 1;
}
```

## S27 NullReturnReplacement

**需求**: 原方法返回 null, 用 [MonoModReplace] 替换为非空值

**期望**: Maybe() == "not-null"

**实际**: not-null

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S27_NullReturn
{
    public string Maybe() => null;
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S27_NullReturn : S27_NullReturn
{
    [MonoModReplace]
    public string Maybe() => "not-null";
}
```

## S29 ReplaceConstructorBody

**需求**: 用 [MonoModReplace]+[MonoModConstructor] 完全替换实例构造函数体, 不调用原 ctor

**期望**: new S29().Tag == "replaced"

**实际**: replaced

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S29_ReplaceConstructor
{
    public string Tag { get; set; } = "unset";

    public S29_ReplaceConstructor()
    {
        Tag = "orig";
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S29_ReplaceConstructor : S29_ReplaceConstructor
{
    [MonoModReplace]
    [MonoModConstructor]
    public void ctor()
    {
        Tag = "replaced";
    }
}
```

## S36 IgnoredHelperType

**需求**: [MonoModIgnore] 标记的整个辅助类型不应被复制进目标程序集, 主方法仍被 patch

**期望**: Run()=="run+p" 且 S36_Helpers 类型不存在于 modded 程序集

**实际**: run=run+p; helperType=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S36_IgnoredHelperType
{
    public string Run() => "run";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S36_IgnoredHelperType : S36_IgnoredHelperType
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+p";
}

[MonoModIgnore]
internal static class S36_Helpers
{
    public static string Tag() => "helper";
}
```

## S38 AddNewConstructorOverload

**需求**: 向目标类型新增一个带参构造函数重载, 并在内部初始化新增字段

**期望**: new S38("hi").Note == "hi"

**实际**: hi

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S38_AddNewConstructor
{
    public S38_AddNewConstructor()
    {
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S38_AddNewConstructor : S38_AddNewConstructor
{
    public string Note;

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    [MonoModConstructor]
    public void ctor(string note)
    {
        orig_ctor();
        Note = note;
    }
}
```

## S39 SealedClassPatch

**需求**: patch 密封类方法: 密封类无法继承, 用 [MonoModPatch] 显式指定目标类型(不继承), 在原返回后追加 !

**期望**: new S39_SealedClass().Name() == "sealed!"

**实际**: sealed!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public sealed class S39_SealedClass
{
    public string Name() => "sealed";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S39_SealedClass")]
internal class patch_S39_SealedClass
{
    public extern string orig_Name();

    public string Name() => orig_Name() + "!";
}
```

## S41 EventRaiseWrap

**需求**: 包装会触发事件的方法, orig_ 仍正确触发事件, 补丁额外累加 Hits

**期望**: 订阅 handler(Hits+=5) 后 Fire() 一次 Hits == 6

**实际**: 6

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S41_EventRaise
{
    public event System.EventHandler? Done;

    public int Hits;

    public void Fire()
    {
        Done?.Invoke(this, System.EventArgs.Empty);
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S41_EventRaise : S41_EventRaise
{
    public extern void orig_Fire();

    public void Fire()
    {
        orig_Fire();
        Hits += 1;
    }
}
```

## S42 ReplacePropertyQuirk

**需求**: 对 patch 中声明为只读的属性施加 [MonoModReplace]: 验证 MonoMod 的实际结构行为 (原属性元数据与 setter/backing 被移除, patch 的 getter 作为独立 get_Label 方法保留并返回新值)

**期望**: get_Label() == "replaced"; Label 属性元数据不存在; set_Label 不存在

**实际**: prop=absent; get_Label=present; set_Label=absent; getVal=replaced

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S42_ReplaceProperty
{
    public string Label { get; set; } = "orig";
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S42_ReplaceProperty : S42_ReplaceProperty
{
    // [MonoModReplace] on a get-only patch property: MonoMod removes the target
    // property metadata, backing field, and setter, but keeps the patch getter as
    // a standalone get_Label method returning the new value. The Label property
    // itself becomes absent on the patched type.
    [MonoModReplace]
    public string Label => "replaced";
}
```

## S43 AsyncMethodWrap

**需求**: 包装 async Task<string> 方法, await orig_ 后在结果追加 !

**期望**: FetchAsync().Result == "fetched!"

**实际**: fetched!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S43_AsyncMethod
{
    public async System.Threading.Tasks.Task<string> FetchAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        return "fetched";
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S43_AsyncMethod : S43_AsyncMethod
{
    public extern System.Threading.Tasks.Task<string> orig_FetchAsync();

    public async System.Threading.Tasks.Task<string> FetchAsync()
    {
        var r = await orig_FetchAsync();
        return r + "!";
    }
}
```

## S44 RefReturnWrap

**需求**: 包装 ref 返回方法, 通过 orig_ 拿到 ref 后修改底层值并返回

**期望**: Slot() 后再读 _v 字段 == 101

**实际**: 101

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S44_RefReturn
{
    private int _v = 1;

    public ref int Slot() => ref _v;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S44_RefReturn : S44_RefReturn
{
    public extern ref int orig_Slot();

    public ref int Slot()
    {
        ref int v = ref orig_Slot();
        v += 100;
        return ref v;
    }
}
```

## S45 InterfaceImplPatch

**需求**: patch 实现接口的类型的虚方法, 通过接口引用调用也应体现补丁

**期望**: ((S45_IShape)new S45_Circle()).Draw() == "[circle]"

**实际**: [circle]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public interface S45_IShape
{
    string Draw();
}

public class S45_Circle : S45_IShape
{
    public string Draw() => "circle";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S45_Circle : S45_Circle
{
    public extern string orig_Draw();

    public string Draw() => "[" + orig_Draw() + "]";
}
```

## S46 RecursiveMethodReentrantWrap

**需求**: 包装递归方法 Fact: orig_ 会重新进入已被 patch 的 Fact, 形成 reentrant 包装 (每层递归都被补丁拦截), 结果偏离原 6 并体现多层包装叠加; 这是 MonoMod orig_ 对递归方法的行为特征

**期望**: Fact(3) != 6 (原值) 且 == 16 (多层 reentrant 叠加)

**实际**: 16

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S46_Recursive
{
    public int Fact(int n) => n <= 1 ? 1 : n * Fact(n - 1);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S46_Recursive : S46_Recursive
{
    public extern int orig_Fact(int n);

    public int Fact(int n)
    {
        // Wrap recursion: orig_ calls the (patched) Fact, so each level adds suffix +1
        return orig_Fact(n) + 1;
    }
}
```

## S47 StaticGenericMethodPatch

**需求**: 包装静态泛型方法 Identity<T>, 在原返回后追加 !

**期望**: Identity<int>(7) == "id:7!"

**实际**: id:7!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S47_StaticGeneric
{
    public static string Identity<T>(T v) => "id:" + v;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S47_StaticGeneric : S47_StaticGeneric
{
    public extern static string orig_Identity<T>(T v);

    public static string Identity<T>(T v) => orig_Identity<T>(v) + "!";
}
```

## S48 GenericConstraintMethodPatch

**需求**: 包装带泛型约束 (where T: IEquatable<T>) 的方法, 约束保持, 追加 !

**期望**: Show<int>(9) == "c:9!"

**实际**: c:9!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S48_Constraint
{
    public string Show<T>(T v) where T : System.IEquatable<T> => "c:" + v;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S48_Constraint : S48_Constraint
{
    public extern string orig_Show<T>(T v) where T : System.IEquatable<T>;

    public string Show<T>(T v) where T : System.IEquatable<T> => orig_Show<T>(v) + "!";
}
```

## S49 RemoveMemberSafe

**需求**: 用 [MonoModRemove] 移除一个无任何方法体引用的成员方法, 保留 Keep() 不变

**期望**: Extra 方法不存在于 modded 类型, Keep()=="keep"

**实际**: keep=keep; extra=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S49_RemoveMember
{
    public string Keep() => "keep";

    // No method body anywhere references Extra; safe for [MonoModRemove].
    public string Extra() => "extra";
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S49_RemoveMember : S49_RemoveMember
{
    [MonoModRemove]
    public string Extra() => "removed";
}
```

## S50 ArrayReturnWrap

**需求**: 包装返回数组的方法, 调用 orig_ 后追加一个元素 3

**期望**: Pair() 序列 == [1,2,3]

**实际**: 1,2,3

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S50_ArrayReturn
{
    public int[] Pair() => new[] { 1, 2 };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S50_ArrayReturn : S50_ArrayReturn
{
    public extern int[] orig_Pair();

    public int[] Pair()
    {
        var a = orig_Pair();
        var r = new int[a.Length + 1];
        for (int i = 0; i < a.Length; i++) r[i] = a[i];
        r[a.Length] = 3;
        return r;
    }
}
```

## S51 IndexerAccessorPatch

**需求**: 分别 patch 索引器的 getter/setter (orig_get_Item / orig_set_Item), get 追加 !, set 写入时附加 #

**期望**: set[0]="x" 后 get[0] == "x#!"

**实际**: x#!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S51_Indexer
{
    private readonly string[] _data = new[] { "a", "b", "c" };

    public string this[int i]
    {
        get => _data[i];
        set => _data[i] = value;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S51_Indexer : S51_Indexer
{
    public extern string orig_get_Item(int i);
    public extern void orig_set_Item(int i, string value);

    public string this[int i]
    {
        get => orig_get_Item(i) + "!";
        set => orig_set_Item(i, value + "#");
    }
}
```

## S52 StaticFieldReadWrap

**需求**: 包装读取静态字段的静态方法, 在原返回上加 1000

**期望**: Counter=0 时 ReadCounter() == 1000

**实际**: 1000

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S52_StaticFieldWrap
{
    public static int Counter = 0;

    public static int ReadCounter() => Counter;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S52_StaticFieldWrap : S52_StaticFieldWrap
{
    public extern static int orig_ReadCounter();

    public static int ReadCounter() => orig_ReadCounter() + 1000;
}
```

## S53 AddPublicAlongsideExplicitInterface

**需求**: 目标类型有显式接口实现 IComparable.CompareTo, patch 向类型新增一个公共 CompareTo 返回固定值, 验证新增公共方法可用且显式接口实现不受影响

**期望**: 新公共 CompareTo(null)==42, 接口路由 ((IComparable)obj).CompareTo(null)==0

**实际**: public=42; iface=0

**结果**: PASS

### 原始目标代码
```csharp
using System;

namespace MonoModTestTargets;

public class S53_ExplicitInterface : IComparable
{
    int IComparable.CompareTo(object obj) => 0;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S53_ExplicitInterface : S53_ExplicitInterface
{
    // The target has no public CompareTo; add one that returns the patched value,
    // demonstrating adding a public method alongside the explicit interface impl.
    public int CompareTo(object obj) => 42;
}
```

## S54 ReadonlyStructMethodPatch

**需求**: patch readonly struct 的方法, 用 [MonoModPatch] 不继承写法 (readonly struct 不可继承), 在原返回上加 1

**期望**: new S54_ReadonlyStruct(5).Double() == 11

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public readonly struct S54_ReadonlyStruct
{
    public readonly int Value;

    public S54_ReadonlyStruct(int v) => Value = v;

    public int Double() => Value * 2;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S54_ReadonlyStruct")]
internal class patch_S54_ReadonlyStruct
{
    public int Value;

    public extern int orig_Double();

    public int Double() => orig_Double() + 1;
}
```

## S55 NullableReturnWrap

**需求**: 包装返回 Nullable<int> 的方法, 原返回非空时加 100, 原返回 null 时仍返回 null

**期望**: Find(3)==103, Find(0)==null

**实际**: find(3)=103; find(0)=null

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S55_NullableReturn
{
    public int? Find(int key) => key > 0 ? key : null;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S55_NullableReturn : S55_NullableReturn
{
    public extern int? orig_Find(int key);

    public int? Find(int key)
    {
        var r = orig_Find(key);
        return r.HasValue ? r.Value + 100 : (int?)null;
    }
}
```

## S57 EnumArgMethodPatch

**需求**: 包装带枚举参数的方法, 在原返回后追加 !

**期望**: Name(S57_Color.Green) == "color:Green!"

**实际**: color:Green!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public enum S57_Color { Red, Green, Blue }

public class S57_EnumArg
{
    public string Name(S57_Color c) => "color:" + c;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S57_EnumArg : S57_EnumArg
{
    public extern string orig_Name(S57_Color c);

    public string Name(S57_Color c) => orig_Name(c) + "!";
}
```

## S58 CrossNamespacePatchType

**需求**: patch_ 类型位于与目标不同的命名空间, MonoMod 按 patch_ 前缀剥离后的简单名映射目标类型

**期望**: new S58_CrossNamespace().Tag() == "sub!"

**实际**: sub!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets.SubNs;

public class S58_CrossNamespace
{
    public string Tag() => "sub";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets.Patches;

// Cross-namespace patch_: MonoMod's patch_ prefix stripping uses the PATCH type's
// namespace to form the target full name, so a patch_ type in a different namespace
// would target a type in the patch's namespace (not the real target). To patch a type
// in another namespace, use [MonoModPatch("global::TargetNs.Type")] explicitly.
[MonoModPatch("global::MonoModTestTargets.SubNs.S58_CrossNamespace")]
internal class S58_CrossNamespacePatch : MonoModTestTargets.SubNs.S58_CrossNamespace
{
    public extern string orig_Tag();

    public string Tag() => orig_Tag() + "!";
}
```

## S59 MultiTypeParamGenericMethod

**需求**: 包装带两个泛型参数的方法 Pair<T,U>, 在原返回外加方括号

**期望**: Pair<int,string>(7,"z") == "[Int32+String:7,z]"

**实际**: [Int32+String:7,z]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S59_MultiTypeParam
{
    public string Pair<T, U>(T a, U b) => typeof(T).Name + "+" + typeof(U).Name + ":" + a + "," + b;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S59_MultiTypeParam : S59_MultiTypeParam
{
    public extern string orig_Pair<T, U>(T a, U b);

    public string Pair<T, U>(T a, U b) => "[" + orig_Pair<T, U>(a, b) + "]";
}
```

## S60 DecimalReturnWrap

**需求**: 包装 decimal 返回方法, 在原结果上加 1m

**期望**: Total(2m, 3m) == 6m

**实际**: 6

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S60_DecimalReturn
{
    public decimal Total(decimal a, decimal b) => a + b;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S60_DecimalReturn : S60_DecimalReturn
{
    public extern decimal orig_Total(decimal a, decimal b);

    public decimal Total(decimal a, decimal b) => orig_Total(a, b) + 1m;
}
```

## S61 CopiedHelperCallable

**需求**: patch 中未标记 [MonoModIgnore] 的辅助方法会被复制进目标类型, 补丁方法体可调用它 (与 S07 IgnoreHelper 对比)

**期望**: Run()=="run+copied" 且 Suffix 方法存在于 modded 类型

**实际**: run=run+copied; suffix=present

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S61_CopiedHelper
{
    public string Run() => "run";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S61_CopiedHelper : S61_CopiedHelper
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+" + Suffix();

    // Not [MonoModIgnore]: this method is copied into the target type and
    // can be called from the patched method body.
    public string Suffix() => "copied";
}
```

## S62 AddStaticFieldInitInCtor

**需求**: 向目标类型新增 static 字段, 并在 patch 构造函数中惰性初始化 (仅首次)

**期望**: 首次 new S62() 后 GlobalTag == "init", 再次 new 仍 == "init"

**实际**: first=init; second=init

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S62_AddStaticField
{
    public S62_AddStaticField() { }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S62_AddStaticField : S62_AddStaticField
{
    public static string GlobalTag;

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        if (GlobalTag is null)
            GlobalTag = "init";
    }
}
```

## S63 LinkFromStaticRelink

**需求**: 用 [MonoModLinkFrom] 静态重链接: 目标 Wrap() 内部调用 Old(), 补丁提供 Replacement() 并声明 LinkFrom Old() 的 findableID, 使 Wrap 内对 Old 的调用被重定向到 Replacement

**期望**: new S63_LinkFrom().Wrap() == "relinked"

**实际**: relinked

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S63_LinkFrom
{
    public string Old() => "old";

    // Wrap calls Old(); after relinking, this call should target the patch replacement.
    public string Wrap() => Old();
}
```

### Patch 代码
```csharp
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S63_LinkFrom : S63_LinkFrom
{
    // Redirect any call to S63_LinkFrom::Old() to this Replacement().
    // FindableID matches MonoMod's GetID: "System.String MonoModTestTargets.S63_LinkFrom::Old()"
    [MonoModLinkFrom("System.String MonoModTestTargets.S63_LinkFrom::Old()")]
    public string Replacement() => "relinked";
}
```

## S64 OperatorOverloadPatch

**需求**: patch 运算符重载方法 op_Addition, 在原结果上加 1

**期望**: (new S64(2) + new S64(3)).Value == 6

**实际**: 6

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S64_OperatorOverload
{
    public int Value;

    public S64_OperatorOverload(int v) => Value = v;

    public static S64_OperatorOverload operator +(S64_OperatorOverload a, S64_OperatorOverload b) =>
        new S64_OperatorOverload(a.Value + b.Value);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S64_OperatorOverload : S64_OperatorOverload
{
    public patch_S64_OperatorOverload(int v) : base(v) { }

    // Declare op_Addition as a plain static method (not C# operator) so the patch
    // type is a valid containing type. MonoMod relinks the method by name/signature.
    public extern static S64_OperatorOverload orig_op_Addition(S64_OperatorOverload a, S64_OperatorOverload b);

    public static S64_OperatorOverload op_Addition(S64_OperatorOverload a, S64_OperatorOverload b)
    {
        var r = orig_op_Addition(a, b);
        r.Value += 1;
        return r;
    }
}
```

## S65 FuncReturnWrap

**需求**: 包装返回 Func<int,int> 的方法, 拿到原委托后返回新委托 (在原结果上加 1)

**期望**: Getter()(5) == 11

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S65_FuncReturn
{
    public System.Func<int, int> Getter() => x => x * 2;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S65_FuncReturn : S65_FuncReturn
{
    public extern System.Func<int, int> orig_Getter();

    public System.Func<int, int> Getter()
    {
        var f = orig_Getter();
        return x => f(x) + 1;
    }
}
```

## S67 ParamsObjectArrayWrap

**需求**: 包装 params object[] 方法, 在原返回后追加 !

**期望**: Join(1,"x",true) == "1,x,True!"

**实际**: 1,x,True!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S67_ParamsObjectArray
{
    public string Join(params object[] parts) => string.Join(",", parts);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S67_ParamsObjectArray : S67_ParamsObjectArray
{
    public extern string orig_Join(object[] parts);

    public string Join(object[] parts) => orig_Join(parts) + "!";
}
```

## S68 TryFinallyCleanup

**需求**: 包装方法用 try/finally, 验证 orig_ 正常返回后 finally 中的清理逻辑执行

**期望**: Render()=="render!" 且 CleanedUp==true

**实际**: render=render!; cleaned=True

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S68_TryFinallyWrap
{
    public bool CleanedUp;

    public string Render() => "render";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S68_TryFinallyWrap : S68_TryFinallyWrap
{
    public extern string orig_Render();

    public string Render()
    {
        try
        {
            return orig_Render() + "!";
        }
        finally
        {
            CleanedUp = true;
        }
    }
}
```

## S69 PatchThrowsException

**需求**: patch 在调用 orig_ 后抛出特定异常, 验证补丁方法体异常语义生效

**期望**: Go() 抛 InvalidOperationException 且消息以 "patched:ok" 开头

**实际**: threw:patched:ok

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S69_ThrowsException
{
    public string Go() => "ok";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S69_ThrowsException : S69_ThrowsException
{
    public extern string orig_Go();

    public string Go()
    {
        var r = orig_Go();
        throw new System.InvalidOperationException("patched:" + r);
    }
}
```

## S70 TypeIdentityPatch

**需求**: patch 调用 orig_(其内部 GetType().Name) 并追加 !, 验证 GetType 在 patched 类型上返回目标类型名

**期望**: TypeName() == "S70_TypeIdentity!"

**实际**: S70_TypeIdentity!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S70_TypeIdentity
{
    public string TypeName() => GetType().Name;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S70_TypeIdentity : S70_TypeIdentity
{
    public extern string orig_TypeName();

    public string TypeName() => orig_TypeName() + "!";
}
```

## S71 ObjectArgMethodWrap

**需求**: 包装 object 参数方法, 在原返回后追加 !

**期望**: Describe(42) == "obj:42!"

**实际**: obj:42!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S71_ObjectArgMethod
{
    public string Describe(object o) => "obj:" + o;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S71_ObjectArgMethod : S71_ObjectArgMethod
{
    public extern string orig_Describe(object o);

    public string Describe(object o) => orig_Describe(o) + "!";
}
```

## S72 SelfAddedMethodInvoke

**需求**: patch 方法体调用同一 patch 新增的实例方法 Extra(), 验证新增方法被复制且可被补丁方法体调用

**期望**: Base() == "base:extra"

**实际**: base=base:extra; extra=present

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S72_SelfAddedMethodInvoke
{
    public string Base() => "base";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S72_SelfAddedMethodInvoke : S72_SelfAddedMethodInvoke
{
    public extern string orig_Base();

    public string Base() => orig_Base() + ":" + Extra();

    // Added method, called from patched Base(); copied into target and callable.
    public string Extra() => "extra";
}
```

## S73 RethrowWrappedException

**需求**: patch 用 try/catch 包装 orig_, 捕获原 FormatException 后重新抛出带前缀消息的新异常

**期望**: Parse("abc") 抛 FormatException 且消息以 "wrapped:" 开头; Parse("42")==42

**实际**: parse42=True; threw:wrapped:The input string 'abc' was not in a correct format.

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S73_RethrowWrap
{
    public int Parse(string s) => int.Parse(s);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S73_RethrowWrap : S73_RethrowWrap
{
    public extern int orig_Parse(string s);

    public int Parse(string s)
    {
        try
        {
            return orig_Parse(s);
        }
        catch (System.FormatException ex)
        {
            throw new System.FormatException("wrapped:" + ex.Message);
        }
    }
}
```

## S74 StaticReadonlyFieldWrap

**需求**: 包装读取 static readonly 字段的方法, 在原返回后追加 !

**期望**: Reveal() == "topsecret!"

**实际**: topsecret!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S74_StaticReadonlyField
{
    public static readonly string Secret = "topsecret";

    public string Reveal() => Secret;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S74_StaticReadonlyField : S74_StaticReadonlyField
{
    public extern string orig_Reveal();

    public string Reveal() => orig_Reveal() + "!";
}
```

## S75 AddedMethodMutatesExistingField

**需求**: patch 新增实例方法 Bump(int), 调用它修改目标类型上已存在的字段 Count

**期望**: Bump(5) 后 Bump(3) 后 Count == 8

**实际**: 8

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S75_AddedMethodMutatesField
{
    public int Count;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S75_AddedMethodMutatesField : S75_AddedMethodMutatesField
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    // Added method mutates an existing field on the target type.
    public void Bump(int n) => Count += n;
}
```

## S76 StringInterpolationWrap

**需求**: 包装使用字符串插值的方法, 在原返回外加方括号

**期望**: Build("x", 9) == "[x-9]"

**实际**: [x-9]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S76_StringInterpolation
{
    public string Build(string a, int b) => $"{a}-{b}";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S76_StringInterpolation : S76_StringInterpolation
{
    public extern string orig_Build(string a, int b);

    public string Build(string a, int b) => "[" + orig_Build(a, b) + "]";
}
```

## S77 ConditionalOrigCall

**需求**: patch 按条件决定是否调用 orig_: 空输入短路返回 "empty", 非空才调用 orig_ 并追加 !

**期望**: Echo("")=="empty", Echo("hi")=="hi!"

**实际**: empty=empty; hi=hi!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S77_ConditionalOrigCall
{
    public string Echo(string s) => s;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S77_ConditionalOrigCall : S77_ConditionalOrigCall
{
    public extern string orig_Echo(string s);

    public string Echo(string s)
    {
        // Only call orig_ for non-empty input; otherwise short-circuit.
        if (string.IsNullOrEmpty(s)) return "empty";
        return orig_Echo(s) + "!";
    }
}
```

## S78 ValueTypeReturnWrap

**需求**: 包装返回自定义 struct 的方法, 修改 struct 字段后返回

**期望**: Build().Code == 11 (原值 1)

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public struct S78_Result
{
    public int Code;
}

public class S78_ValueTypeReturn
{
    public S78_Result Build() => new S78_Result { Code = 1 };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S78_ValueTypeReturn : S78_ValueTypeReturn
{
    public extern S78_Result orig_Build();

    public S78_Result Build()
    {
        var r = orig_Build();
        r.Code += 10;
        return r;
    }
}
```

## S79 ListReturnWrap

**需求**: 包装返回 List<int> 的方法, 调用 orig_ 后向列表追加元素 4

**期望**: Three() 序列 == [1,2,3,4]

**实际**: 1,2,3,4

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S79_ListReturn
{
    public System.Collections.Generic.List<int> Three() => new() { 1, 2, 3 };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S79_ListReturn : S79_ListReturn
{
    public extern System.Collections.Generic.List<int> orig_Three();

    public System.Collections.Generic.List<int> Three()
    {
        var list = orig_Three();
        list.Add(4);
        return list;
    }
}
```

## S80 ConstFieldAddCopied

**需求**: patch 新增 const 字段, 验证 const 值作为元数据被复制 (与普通字段初始化器不复制形成对比)

**期望**: Label() == "label:EXTRA"

**实际**: label:EXTRA

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S80_ConstFieldAdd
{
    public string Label() => "label";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S80_ConstFieldAdd : S80_ConstFieldAdd
{
    // const fields: their constant values ARE copied (metadata, not ctor IL).
    public const string Extra = "EXTRA";

    public extern string orig_Label();

    public string Label() => orig_Label() + ":" + Extra;
}
```

## S81 TupleReturnWrap

**需求**: 包装返回值元组 (int,string) 的方法, 对各分量分别变换

**期望**: Pair() == (11, "x!")

**实际**: a=11; b=x!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S81_TupleReturn
{
    public (int a, string b) Pair() => (1, "x");
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S81_TupleReturn : S81_TupleReturn
{
    public extern (int a, string b) orig_Pair();

    public (int a, string b) Pair()
    {
        var t = orig_Pair();
        return (t.a + 10, t.b + "!");
    }
}
```

## S82 PrivateStaticMethodPatch

**需求**: patch 私有静态方法 Secret, 通过公共方法 Reveal 间接验证 (与 S13 私有实例方法互补, 这次是 static)

**期望**: Reveal() == "secret!"

**实际**: secret!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S82_PrivateStaticMethod
{
    public string Reveal() => Secret();

    private static string Secret() => "secret";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S82_PrivateStaticMethod : S82_PrivateStaticMethod
{
    private extern static string orig_Secret();

    private static string Secret() => orig_Secret() + "!";
}
```

## S83 IEnumerableYieldWrap

**需求**: 包装返回 IEnumerable<int> 的方法, 用 yield return 对每个元素加 100

**期望**: Range() 序列 == [101,102,103]

**实际**: 101,102,103

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S83_IEnumerableReturn
{
    public System.Collections.Generic.IEnumerable<int> Range() => new[] { 1, 2, 3 };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S83_IEnumerableReturn : S83_IEnumerableReturn
{
    public extern System.Collections.Generic.IEnumerable<int> orig_Range();

    public System.Collections.Generic.IEnumerable<int> Range()
    {
        foreach (var v in orig_Range())
            yield return v + 100;
    }
}
```

## S84 GenericParamsMethodWrap

**需求**: 包装同时带泛型参数和 params 数组的方法, 在原返回外加方括号

**期望**: Compose<int>("p", 1, 2) == "[p:1,2]"

**实际**: [p:1,2]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S84_GenericParamsMethod
{
    public string Compose<T>(string prefix, params T[] items) => prefix + ":" + string.Join(",", items);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S84_GenericParamsMethod : S84_GenericParamsMethod
{
    public extern string orig_Compose<T>(string prefix, T[] items);

    public string Compose<T>(string prefix, T[] items) => "[" + orig_Compose<T>(prefix, items) + "]";
}
```

## S85 NestedPrivateTypePatch

**需求**: patch 私有嵌套类型 Inner 的方法, 通过公共方法 Access 间接验证

**期望**: Access() == "inner!"

**实际**: inner!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S85_NestedPrivateOwner
{
    internal class Inner
    {
        public string Id() => "inner";
    }

    public string Access() => new Inner().Id();
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S85_NestedPrivateOwner : S85_NestedPrivateOwner
{
    internal class patch_Inner : S85_NestedPrivateOwner.Inner
    {
        public extern string orig_Id();

        public string Id() => orig_Id() + "!";
    }
}
```

## S86 LockStatementWrap

**需求**: 包装含 lock 语句的方法, orig_ 内部的 lock 仍正确执行, 补丁在外层加 1

**期望**: Run() == 43 (原值 42)

**实际**: 43

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S86_LockStatement
{
    private readonly object _gate = new();

    public int Run()
    {
        lock (_gate)
        {
            return 42;
        }
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S86_LockStatement : S86_LockStatement
{
    public extern int orig_Run();

    public int Run() => orig_Run() + 1;
}
```

## S87 BaseVirtualPatchAffectsDerived

**需求**: patch 基类虚方法, 派生类 base.Name() 调用基类实现, patch 后 base.Name() 返回带补丁的值, 派生类 override 串联体现补丁

**期望**: new S87_Derived().Name() == "derived:base!", new S87_BaseVirtual().Name() == "base!"

**实际**: derived=derived:base!; base=base!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S87_BaseVirtual
{
    public virtual string Name() => "base";
}

public class S87_Derived : S87_BaseVirtual
{
    public override string Name() => "derived:" + base.Name();
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S87_BaseVirtual : S87_BaseVirtual
{
    public extern string orig_Name();

    public override string Name() => orig_Name() + "!";
}
```

## S88 EarlyReturnNoOrig

**需求**: 完全替换方法体 (无 orig_), 负码短路返回 -1, 正码返回原逻辑+1

**期望**: Handle(-5)==-1, Handle(5)==11

**实际**: neg=-1; pos=11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S88_EarlyReturnNoOrig
{
    public int Handle(int code) => code * 2;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S88_EarlyReturnNoOrig : S88_EarlyReturnNoOrig
{
    // No orig_ declared: full replacement. Negative code short-circuits to -1.
    public int Handle(int code) => code < 0 ? -1 : code * 2 + 1;
}
```

## S89 UsingDisposePatternWrap

**需求**: 包装含 using (IDisposable) 语句的方法, orig_ 内 using 仍正确 Dispose, 补丁追加 !

**期望**: Run() == "ran!"

**实际**: ran!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S89_UsingDisposePattern
{
    public string Run()
    {
        using var scope = new S89_Scope();
        return "ran";
    }
}

public sealed class S89_Scope : System.IDisposable
{
    public bool Disposed;

    public void Dispose() => Disposed = true;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S89_UsingDisposePattern : S89_UsingDisposePattern
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S90 DictionaryReturnWrap

**需求**: 包装返回 Dictionary<string,int> 的方法, 调用 orig_ 后新增键 b=2

**期望**: Build() 含 a==1 且 b==2

**实际**: a=1; hasB=True

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S90_DictionaryReturn
{
    public System.Collections.Generic.Dictionary<string, int> Build() => new() { ["a"] = 1 };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S90_DictionaryReturn : S90_DictionaryReturn
{
    public extern System.Collections.Generic.Dictionary<string, int> orig_Build();

    public System.Collections.Generic.Dictionary<string, int> Build()
    {
        var d = orig_Build();
        d["b"] = 2;
        return d;
    }
}
```

## S91 StructParamMethodWrap

**需求**: 包装带自定义 struct 参数的方法 (与 S78 struct 返回互补, 这次是 struct 作为参数), 调用 orig_ 后加 10

**期望**: Read(handle{Value=5}) == 15

**实际**: 15

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public struct S91_Handle
{
    public int Value;
}

public class S91_StructParamMethod
{
    public int Read(S91_Handle h) => h.Value;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S91_StructParamMethod : S91_StructParamMethod
{
    public extern int orig_Read(S91_Handle h);

    public int Read(S91_Handle h) => orig_Read(h) + 10;
}
```

## S92 StaticFieldInitInCctor

**需求**: patch 新增 static 字段并在静态构造函数 cctor 中初始化, Read() 读取它叠加到 orig_ 结果

**期望**: Read() == 7 (orig_ 返回 0 + Cache=7)

**实际**: 7

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S92_StaticFieldCrossMethod
{
    public static int Base = 0;

    static S92_StaticFieldCrossMethod()
    {
        Base = 0;
    }

    public static int Read() => Base;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S92_StaticFieldCrossMethod : S92_StaticFieldCrossMethod
{
    public static int Cache;

    public extern static int orig_Read();

    public static int Read() => orig_Read() + Cache;

    public extern static void orig_cctor();

    [MonoModConstructor]
    public static void cctor()
    {
        orig_cctor();
        Cache = 7;
    }
}
```

## S93 RefReadonlyParamWrap

**需求**: 包装带 in (ref readonly) 双参数的方法, 调用 orig_ 后加 100

**期望**: Sum(2, 3) == 105

**实际**: 105

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S93_RefReadonlyParam
{
    public int Sum(in int a, in int b) => a + b;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S93_RefReadonlyParam : S93_RefReadonlyParam
{
    public extern int orig_Sum(in int a, in int b);

    public int Sum(in int a, in int b) => orig_Sum(in a, in b) + 100;
}
```

## S94 TwoDimArrayReturnWrap

**需求**: 包装返回二维数组 int[,] 的方法, 调用 orig_ 后每个元素加 1

**期望**: Grid() == [[2,3],[4,5]]

**实际**: 2,3,4,5

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S94_TwoDimArrayReturn
{
    public int[,] Grid() => new int[,] { { 1, 2 }, { 3, 4 } };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S94_TwoDimArrayReturn : S94_TwoDimArrayReturn
{
    public extern int[,] orig_Grid();

    public int[,] Grid()
    {
        var g = orig_Grid();
        // bump every cell by 1
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                g[i, j] += 1;
        return g;
    }
}
```

## S95 AddGetterOnlyProperty

**需求**: patch 新增只读计算属性 (无 setter), 基于目标方法 Base() 计算

**期望**: Doubled == 2 且 Doubled 无 setter

**实际**: doubled=2; setter=absent

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S95_AddGetterOnlyProperty
{
    public int Base() => 1;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S95_AddGetterOnlyProperty : S95_AddGetterOnlyProperty
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    // Added getter-only property (computed from Base()).
    public int Doubled => Base() * 2;
}
```

## S96 StackallocLocalWrap

**需求**: 包装含 stackalloc/Span 局部的方法, orig_ 内 stackalloc 仍正确, 补丁加 1

**期望**: Total(7) == 8

**实际**: 8

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S96_StackallocLocal
{
    public int Total(int n)
    {
        Span<int> buf = stackalloc int[1];
        buf[0] = n;
        return buf[0];
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S96_StackallocLocal : S96_StackallocLocal
{
    public extern int orig_Total(int n);

    public int Total(int n) => orig_Total(n) + 1;
}
```

## S97 ForceCallvirtOnNonVirtual

**需求**: patch 方法施加 [MonoModForceCallvirt], 验证补丁后程序集可加载且方法行为正确 (调用约定强制为 callvirt 不破坏语义)

**期望**: Compute() == 15 (原值 10 + 5)

**实际**: 15

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S97_ForceCallNonVirtual
{
    public int Compute() => 10;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S97_ForceCallNonVirtual : S97_ForceCallNonVirtual
{
    public extern int orig_Compute();

    // [MonoModForceCallvirt] forces calls to Compute to use callvirt (null-check).
    // The patched method itself calls orig_Compute; behavior must still be correct.
    [MonoModForceCallvirt]
    public int Compute() => orig_Compute() + 5;
}
```

## S98 IfFlagConditionalInclude

**需求**: 用 [MonoModIfFlag("s98_on", true)] 条件 patch: harness 设置 s98_on=true 时补丁生效, Run() 追加 !

**期望**: Run() == "orig!"

**实际**: orig!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S98_IfFlagInclude
{
    public string Run() => "orig";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModIfFlag("s98_on", true)]
internal class patch_S98_IfFlagInclude : S98_IfFlagInclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S99 IfFlagConditionalExclude

**需求**: 用 [MonoModIfFlag("s99_on", false)] 条件 patch: harness 未设置 s99_on, fallback=false, 补丁被跳过, Run() 保持原值

**期望**: Run() == "orig" (补丁未生效)

**实际**: orig

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S99_IfFlagExclude
{
    public string Run() => "orig";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModIfFlag("s99_on", false)]
internal class patch_S99_IfFlagExclude : S99_IfFlagExclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S100 ForceCallOnVirtualMethod

**需求**: patch override 虚方法并施加 [MonoModForceCall], 强制对 Compute 的调用用 call (非虚分派), 验证补丁后行为正确

**期望**: Compute() == 15 (原值 10 + 5)

**实际**: 15

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S100_ForceCallVirtual
{
    public virtual int Compute() => 10;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S100_ForceCallVirtual : S100_ForceCallVirtual
{
    public extern int orig_Compute();

    // [MonoModForceCall] forces calls to Compute to use call (non-virtual dispatch).
    [MonoModForceCall]
    public override int Compute() => orig_Compute() + 5;
}
```

## S101 LinkToReverseRelinkRegistration

**需求**: 用 [MonoModLinkTo] 注册反向重链接 (将 Replacement 的调用重定向到 Source), 验证补丁流程不破坏且 Source 仍被 patch

**期望**: Source() == "source!" (LinkTo 注册不影响 Source patch)

**实际**: source!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S101_LinkToReverse
{
    public string Source() => "source";

    public string Call() => Source();
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S101_LinkToReverse : S101_LinkToReverse
{
    // [MonoModLinkTo] on a patch method M: relinks calls TO M to the specified target.
    // Here Replacement is called by nobody; instead we mark the existing Source wrapper.
    // Demonstrate LinkTo by adding Replacement and marking it LinkTo to Source:
    // any call to Replacement becomes a call to Source. Call() still calls Source directly,
    // so we verify LinkTo registration doesn't break patching and Source still works.
    [MonoModLinkTo("MonoModTestTargets.S101_LinkToReverse", "Source")]
    public string Replacement() => "should-be-relinked";

    public extern string orig_Source();

    public string Source() => orig_Source() + "!";
}
```

## S102 TargetModuleMatch

**需求**: 用 [MonoModTargetModule("MonoModTestTargets")] 条件 patch: 目标程序集名匹配, 补丁生效

**期望**: Run() == "orig!"

**实际**: orig!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S102_TargetModuleMatch
{
    public string Run() => "orig";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModTargetModule("MonoModTestTargets")]
internal class patch_S102_TargetModuleMatch : S102_TargetModuleMatch
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S103 TargetModuleExclude

**需求**: 用 [MonoModTargetModule("SomeOtherAssembly")] 条件 patch: 目标程序集名不匹配, 补丁被跳过, Run() 保持原值

**期望**: Run() == "orig" (补丁未生效)

**实际**: orig

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S103_TargetModuleExclude
{
    public string Run() => "orig";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

// Target module name deliberately does NOT match the real target assembly,
// so MatchingConditionals returns false and the patch is skipped.
[MonoModTargetModule("SomeOtherAssembly")]
internal class patch_S103_TargetModuleExclude : S103_TargetModuleExclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S104 OnPlatformAlwaysExcludesBug

**需求**: 用 [MonoModOnPlatform(OSKind.Windows)] 条件 patch: 发现 MonoMod.Patcher 25.0.1 的 OnPlatform 逻辑 bug — 即使当前平台匹配, 非空平台列表也会被无条件排除 (MatchingConditionals 循环无 break, 循环后 status &= plats.Length==0 总使非空列表为 false). 因此补丁未生效, Run() 保持原值

**期望**: Run() == "orig" (补丁因 OnPlatform bug 未生效, 即使在 Windows 上)

**实际**: run=orig; os=Win32NT

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S104_OnPlatformWindows
{
    public string Run() => "orig";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;
using MonoMod.Utils;

namespace MonoModTestTargets;

[MonoModOnPlatform(OSKind.Windows)]
internal class patch_S104_OnPlatformWindows : S104_OnPlatformWindows
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S105 ForeachMethodWrap

**需求**: 包装含 foreach 遍历的方法, orig_ 内 foreach 仍正确, 补丁加 1

**期望**: Sum([1,2,3]) == 7 (原 6 + 1)

**实际**: 7

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S105_ForeachMethod
{
    public int Sum(int[] values)
    {
        var total = 0;
        foreach (var v in values) total += v;
        return total;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S105_ForeachMethod : S105_ForeachMethod
{
    public extern int orig_Sum(int[] values);

    public int Sum(int[] values) => orig_Sum(values) + 1;
}
```

## S106 CrossAssemblyDependencyStaging

**需求**: 目标方法调用另一程序集 (MonoModHelperLib) 的类型; 补丁包装该方法, 验证跨程序集依赖被正确暂存到 staging 目录, MonoMod 解析器能找到依赖并完成补丁

**期望**: Compute(5) == 11 (HelperMath.Double(5)=10 + 1)

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S106_CrossAssemblyDep
{
    public int Compute(int x) => MonoModHelperLib.HelperMath.Double(x);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S106_CrossAssemblyDep : S106_CrossAssemblyDep
{
    public extern int orig_Compute(int x);

    public int Compute(int x) => orig_Compute(x) + 1;
}
```

## S107 NullableRefParamWrap

**需求**: 包装带 nullable 引用类型参数 (string?) 的方法, 在原返回后追加 !

**期望**: Greet(null)=="hi anon!", Greet("x")=="hi x!"

**实际**: null=hi anon!; x=hi x!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S107_NullableRefParam
{
    public string Greet(string? name) => "hi " + (name ?? "anon");
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S107_NullableRefParam : S107_NullableRefParam
{
    public extern string orig_Greet(string? name);

    public string Greet(string? name) => orig_Greet(name) + "!";
}
```

## S108 SingletonPatternWrap

**需求**: 包装单例类型的实例方法, 验证单例静态属性与实例方法 patch 共存

**期望**: S108_Singleton.Instance.Tag() == "singleton!"

**实际**: singleton!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S108_Singleton
{
    public static S108_Singleton Instance { get; } = new();

    public string Tag() => "singleton";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S108_Singleton : S108_Singleton
{
    public extern string orig_Tag();

    public string Tag() => orig_Tag() + "!";
}
```

## S109 ExplicitInterfaceAddPublic

**需求**: 目标有显式接口实现 S109_IFoo.Bar, patch 新增公共 Bar 返回不同值, 验证显式接口路由不受影响

**期望**: 公共 Bar()=="public-bar", 接口 ((S109_IFoo)obj).Bar()=="bar"

**实际**: public=public-bar; iface=bar

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public interface S109_IFoo
{
    string Bar();
}

public class S109_ExplicitInterfaceMethod : S109_IFoo
{
    string S109_IFoo.Bar() => "bar";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S109_ExplicitInterfaceMethod : S109_ExplicitInterfaceMethod
{
    // Add a public Bar that returns a different value; the explicit interface
    // impl (S109_IFoo.Bar) keeps returning "bar" (not affected by added public Bar).
    public string Bar() => "public-bar";
}
```

## S110 LinqMethodWrap

**需求**: 包装使用 LINQ (Where+Sum) 的方法, orig_ 内 LINQ 仍正确, 补丁加 1

**期望**: SumEvens([1,2,3,4]) == 7 (偶数和 6 + 1)

**实际**: 7

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S110_LinqMethod
{
    public int SumEvens(int[] values) => values.Where(v => v % 2 == 0).Sum();
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S110_LinqMethod : S110_LinqMethod
{
    public extern int orig_SumEvens(int[] values);

    public int SumEvens(int[] values) => orig_SumEvens(values) + 1;
}
```

## S111 ByRefReturnFieldMutation

**需求**: 包装 ref 返回方法, 通过 orig_ 拿到 ref 后修改底层私有字段 (与 S44 互补, 这次字段私有)

**期望**: Value() 后读 _v == 51 (原 1 + 50)

**实际**: 51

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S111_ByRefPropertyField
{
    private int _v = 1;

    public ref int Value() => ref _v;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S111_ByRefPropertyField : S111_ByRefPropertyField
{
    public extern ref int orig_Value();

    public ref int Value()
    {
        ref int v = ref orig_Value();
        v += 50;
        return ref v;
    }
}
```

## S112 GotoLabelWrap

**需求**: 包装含 goto/label 控制流的方法, orig_ 内 goto 循环仍正确, 补丁加 1

**期望**: Loop(3) == 7 (1+2+3=6 + 1)

**实际**: 7

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S112_GotoLabel
{
    public int Loop(int n)
    {
        int sum = 0;
    start:
        if (n <= 0) return sum;
        sum += n;
        n--;
        goto start;
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S112_GotoLabel : S112_GotoLabel
{
    public extern int orig_Loop(int n);

    public int Loop(int n) => orig_Loop(n) + 1;
}
```

## S113 CtorBaseArgsPatch

**需求**: patch 派生类构造函数, 调用 orig_ctor (内部 base(tag)) 后追加 ! 到 Tag

**期望**: new S113_Derived("x").Tag == "x!"

**实际**: x!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S113_Base
{
    public string Tag;

    public S113_Base(string tag) => Tag = tag;
}

public class S113_Derived : S113_Base
{
    public S113_Derived(string tag) : base(tag) { }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S113_Derived : S113_Derived
{
    // C#-visible ctor chains to base; MonoMod patches the .ctor via the ctor method below.
    public patch_S113_Derived(string tag) : base(tag) { }

    public extern void orig_ctor(string tag);

    [MonoModConstructor]
    public void ctor(string tag)
    {
        orig_ctor(tag);
        Tag = Tag + "!";
    }
}
```

## S114 ExtensionMethodPatch

**需求**: patch 静态扩展方法 Shout, 在原返回后追加 !

**期望**: "hi".Shout() == "HI!"

**实际**: HI!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public static class S114_Extensions
{
    public static string Shout(this string s) => s.ToUpperInvariant();
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

// Static class cannot be inherited; use [MonoModPatch] without inheritance.
[MonoModPatch("global::MonoModTestTargets.S114_Extensions")]
internal class patch_S114_Extensions
{
    public extern static string orig_Shout(string s);

    public static string Shout(string s) => orig_Shout(s) + "!";
}
```

## S115 JaggedArrayReturnWrap

**需求**: 包装返回交错数组 int[][] 的方法, 调用 orig_ 后追加子数组 [9]

**期望**: Build() 第三子数组 == [9]

**实际**: len=3; last=9

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S115_JaggedArrayReturn
{
    public int[][] Build() => new[] { new[] { 1 }, new[] { 2, 3 } };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S115_JaggedArrayReturn : S115_JaggedArrayReturn
{
    public extern int[][] orig_Build();

    public int[][] Build()
    {
        var a = orig_Build();
        // append a new sub-array
        var r = new int[a.Length + 1][];
        for (int i = 0; i < a.Length; i++) r[i] = a[i];
        r[a.Length] = new[] { 9 };
        return r;
    }
}
```

## S116 SwitchExpressionWrap

**需求**: 包装使用 switch 表达式的方法, 在原返回外加方括号

**期望**: Classify(0)=="[zero]", Classify(5)=="[many]"

**实际**: c0=[zero]; c5=[many]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S116_SwitchExpression
{
    public string Classify(int n) => n switch
    {
        0 => "zero",
        1 => "one",
        _ => "many",
    };
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S116_SwitchExpression : S116_SwitchExpression
{
    public extern string orig_Classify(int n);

    public string Classify(int n) => "[" + orig_Classify(n) + "]";
}
```

## S117 CheckedContextWrap

**需求**: 包装含 checked 上下文的方法, orig_ 内 checked 仍正确, 补丁加 1

**期望**: Mul(3, 4) == 13 (12 + 1)

**实际**: 13

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S117_CheckedContext
{
    public int Mul(int a, int b)
    {
        checked { return a * b; }
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S117_CheckedContext : S117_CheckedContext
{
    public extern int orig_Mul(int a, int b);

    public int Mul(int a, int b) => orig_Mul(a, b) + 1;
}
```

## S118 LocalFunctionWrap

**需求**: 包装含局部函数的方法, orig_ 内局部函数仍正确, 补丁加 1

**期望**: Compute(5) == 11 (5*2=10 + 1)

**实际**: 11

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S118_LocalFunction
{
    public int Compute(int n)
    {
        int Doubler(int x) => x * 2;
        return Doubler(n);
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S118_LocalFunction : S118_LocalFunction
{
    public extern int orig_Compute(int n);

    public int Compute(int n) => orig_Compute(n) + 1;
}
```

## S119 InitOnlyPropertyWrap

**需求**: patch 含 init-only 属性的类的方法, 在原返回后追加 !

**期望**: new S119_InitOnly().Greet() == "hi anon!"

**实际**: hi anon!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S119_InitOnly
{
    public string Name { get; init; }

    public S119_InitOnly() => Name = "anon";

    public string Greet() => "hi " + Name;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S119_InitOnly : S119_InitOnly
{
    public extern string orig_Greet();

    public string Greet() => orig_Greet() + "!";
}
```

## S120 DelegateFieldInvocationWrap

**需求**: 包装调用委托字段的方法, orig_ 内委托字段调用仍正确, 补丁加 10

**期望**: Apply(5) == 16 (5+1=6 + 10)

**实际**: 16

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S120_DelegateField
{
    public System.Func<int, int> Transform = x => x + 1;

    public int Apply(int n) => Transform(n);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S120_DelegateField : S120_DelegateField
{
    public extern int orig_Apply(int n);

    public int Apply(int n) => orig_Apply(n) + 10;
}
```

## S121 TryFinallyNoUsingWrap

**需求**: 包装含 try/finally (无 using) 的方法, orig_ 内 finally 仍执行, 补丁追加 !

**期望**: Run() == "ran!" 且 CleanedUp==true

**实际**: run=ran!; cleaned=True

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S121_TryFinallyNoUsing
{
    public bool CleanedUp;

    public string Run()
    {
        try
        {
            return "ran";
        }
        finally
        {
            CleanedUp = true;
        }
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S121_TryFinallyNoUsing : S121_TryFinallyNoUsing
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}
```

## S122 NonGenericTaskWrap

**需求**: 包装非泛型 async Task 方法, await orig_ 后设置完成标志

**期望**: DoAsync().Wait() 完成后 Completed==true

**实际**: True

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S122_NonGenericTaskReturn
{
    public async System.Threading.Tasks.Task DoAsync()
    {
        await System.Threading.Tasks.Task.Yield();
    }
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S122_NonGenericTaskReturn : S122_NonGenericTaskReturn
{
    public static bool Completed;

    public extern System.Threading.Tasks.Task orig_DoAsync();

    public async System.Threading.Tasks.Task DoAsync()
    {
        await orig_DoAsync();
        Completed = true;
    }
}
```

## S123 LazyFieldMethodWrap

**需求**: 包装使用 Lazy<int> 字段的方法, orig_ 内 Lazy.Value 仍正确, 补丁加 1

**期望**: Get() == 43 (原 42 + 1)

**实际**: 43

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S123_LazyFieldMethod
{
    private readonly System.Lazy<int> _val = new(() => 42);

    public int Get() => _val.Value;
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S123_LazyFieldMethod : S123_LazyFieldMethod
{
    public extern int orig_Get();

    public int Get() => orig_Get() + 1;
}
```

## S124 NestedTernaryWrap

**需求**: 包装含嵌套三元表达式的方法, 在原返回外加方括号

**期望**: Classify(0)=="[zero]", Classify(-1)=="[neg]", Classify(5)=="[pos]"

**实际**: 0=[zero]; -1=[neg]; 5=[pos]

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S124_NestedTernary
{
    public string Classify(int n) => n == 0 ? "zero" : n < 0 ? "neg" : "pos";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S124_NestedTernary : S124_NestedTernary
{
    public extern string orig_Classify(int n);

    public string Classify(int n) => "[" + orig_Classify(n) + "]";
}
```

## S125 StringConcatMultiArgWrap

**需求**: 包装使用 string.Concat 多参数的方法, 在原返回后追加 !

**期望**: Build("a", 7, true) == "a-7-True!"

**实际**: a-7-True!

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S125_StringConcatMultiArg
{
    public string Build(string a, int b, bool c) => string.Concat(a, "-", b, "-", c);
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S125_StringConcatMultiArg : S125_StringConcatMultiArg
{
    public extern string orig_Build(string a, int b, bool c);

    public string Build(string a, int b, bool c) => orig_Build(a, b, c) + "!";
}
```

## S126 AddNewEventAndFire

**需求**: patch 向目标类型新增 event 并新增 Fire() 方法触发它, 验证新增事件可订阅与触发

**期望**: 订阅 handler(累加 5) 后 Fire() 一次 Hits == 5

**实际**: hits=5

**结果**: PASS

### 原始目标代码
```csharp
namespace MonoModTestTargets;

public class S126_AddNewEvent
{
    public string Base() => "base";
}
```

### Patch 代码
```csharp
#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S126_AddNewEvent : S126_AddNewEvent
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    public event System.EventHandler? Done;

    public void Fire() => Done?.Invoke(this, System.EventArgs.Empty);
}
```

## 汇总

- 通过: 119
- 失败: 0
- 总计: 119
