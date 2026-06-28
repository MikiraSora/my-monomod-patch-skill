using MonoModDiffClassifier;

namespace MonoModDiffClassifier.Tests;

/// <summary>
/// 50 例 git diff→patch 分类测试夹具，对应 tests/git-diff-cases.md。
/// 每例给出 base/head 源码（多行真实 C# 风格，使行级 diff 接近真实 git 输出）
/// 与期望分类（取该例判定的“主”分类）。
/// </summary>
internal static class Cases
{
    public sealed record Case(string Id, string Name, string Base, string Head, DiffCategory Expected, string FileName = "Sample.cs");

    public static List<Case> All() => new()
    {
        // ===== L1 单一原子改动（1-10）=====
        new("01","BODY 返回值加工",
            "public string Greet(string name) => \"hi \" + name;",
            "public string Greet(string name) => \"hello \" + name.ToUpperInvariant();",
            DiffCategory.Body),

        new("02","BODY void 副作用变更",
            "public void Log(string m)\n{\n    Console.WriteLine(m);\n}",
            "public void Log(string m)\n{\n    Console.WriteLine(\"[DBG] \" + m);\n}",
            DiffCategory.Body),

        new("03","BODY 静态方法体变更",
            "public static int Id(int x) => x;",
            "public static int Id(int x) => x * 2;",
            DiffCategory.Body),

        new("04","NEW 新增实例方法",
            "public class Foo\n{\n    public int A() => 1;\n}",
            "public class Foo\n{\n    public int A() => 1;\n    public int B() => 2;\n}",
            DiffCategory.New),

        new("05","NEW 新增静态字段",
            "public class Counter\n{\n    public int N;\n}",
            "public class Counter\n{\n    public int N;\n    public static int Total;\n}",
            DiffCategory.New),

        new("06","NEW 新增独立类型",
            "namespace App;",
            "namespace App;\n\npublic class Logger\n{\n    public void Write(string s) {}\n}",
            DiffCategory.New),

        new("07","ACC private→public",
            "private void Reset() { }",
            "public void Reset() { }",
            DiffCategory.Acc),

        new("08","CONST 改 const 值",
            "public const int MaxRetry = 3;",
            "public const int MaxRetry = 5;",
            DiffCategory.Const),

        new("09","BODY 构造函数体变更(末尾追加)",
            "public Widget(string n)\n{\n    Name = n;\n}",
            "public Widget(string n)\n{\n    Name = n;\n    Tag = \"default\";\n}",
            DiffCategory.Body),

        new("10","NEW 新增自动属性",
            "public class Node\n{\n    public int Value;\n}",
            "public class Node\n{\n    public int Value;\n    public string Label { get; set; }\n}",
            DiffCategory.New),

        // ===== L2 组合改动（11-20）=====
        new("11","BODY×2 同类型两方法体",
            "public int Add(int a, int b) => a + b;\npublic int Sub(int a, int b) => a - b;",
            "public int Add(int a, int b) => a + b + 1;\npublic int Sub(int a, int b) => a - b - 1;",
            DiffCategory.Body),

        new("12","NEW+BODY 新增方法并改另一方法",
            "public string Format(string s) => s;",
            "public string Format(string s) => s.Trim();\npublic string Decorate(string s) => \"[\" + s + \"]\";",
            DiffCategory.Body),

        new("13","INIT 自动属性初始化器",
            "public string Mode { get; set; } = \"auto\";",
            "public string Mode { get; set; } = \"manual\";",
            DiffCategory.Init),

        new("14","NEW 带初始化器新增字段",
            "public class Cfg\n{\n}",
            "public class Cfg\n{\n    public string Tag = \"x\";\n}",
            DiffCategory.New),

        new("15","CONST×2 两个 const 值",
            "public const int A = 1;\npublic const int B = 2;",
            "public const int A = 10;\npublic const int B = 20;",
            DiffCategory.Const),

        new("16","ACC+BODY 可访问性并改体",
            "private int Calc(int x) => x;",
            "public int Calc(int x) => x + 1;",
            DiffCategory.Acc),

        new("17","NEW 新增构造函数",
            "public class Box\n{\n    public Box(int w) { W = w; }\n    public int W;\n}",
            "public class Box\n{\n    public Box(int w) { W = w; }\n    public Box(int w, int h) : this(w) { H = h; }\n    public int W;\n    public int H;\n}",
            DiffCategory.New),

        new("18","REMOVE 删除方法",
            "public class C\n{\n    public void Debug() { }\n}",
            "public class C\n{\n}",
            DiffCategory.Remove),

        new("19","NEW 新增嵌套类",
            "public class Outer\n{\n}",
            "public class Outer\n{\n    public class Inner\n    {\n        public int V;\n    }\n}",
            DiffCategory.New),

        new("20","BODY 泛型方法体变更",
            "public T Pick<T>(T a, T b) => a;",
            "public T Pick<T>(T a, T b) => b;",
            DiffCategory.Body),

        // ===== L3 签名变更硬限制（21-28）=====
        new("21","SIG 加参数",
            "public void TakeDamage(int amount) { }",
            "public void TakeDamage(int amount, bool ignoreArmor) { }",
            DiffCategory.Sig),

        new("22","SIG 删参数",
            "public void Send(string msg, int prio) { }",
            "public void Send(string msg) { }",
            DiffCategory.Sig),

        new("23","SIG 改返回类型",
            "public int Count() => 0;",
            "public long Count() => 0L;",
            DiffCategory.Sig),

        new("24","SIG 加泛型参数",
            "public T Make<T>() => default;",
            "public T Make<T, U>() => default;",
            DiffCategory.Sig),

        new("25","SIG instance→static",
            "public int Value() => 42;",
            "public static int Value() => 42;",
            DiffCategory.Sig),

        new("26","SIG ref→out",
            "public void Mutate(ref int x) { }",
            "public void Mutate(out int x) { x = 0; }",
            DiffCategory.Sig),

        new("27","SIG 删 in 修饰",
            "public void Use(in int x) { }",
            "public void Use(int x) { }",
            DiffCategory.Sig),

        new("28","SIG 改属性类型",
            "public int Port { get; set; }",
            "public string Port { get; set; }",
            DiffCategory.Sig),

        // ===== L4 方法体中间插入（29-38）=====
        new("29","MID 两调用间插入日志",
            "public void Run()\n{\n    A();\n    C();\n}",
            "public void Run()\n{\n    A();\n    B();\n    C();\n}",
            DiffCategory.Mid),

        new("30","MID 循环体内插入",
            "public int Sum(int[] xs)\n{\n    int s = 0;\n    for (int i = 0; i < xs.Length; i++)\n    {\n        s += xs[i];\n    }\n    return s;\n}",
            "public int Sum(int[] xs)\n{\n    int s = 0;\n    for (int i = 0; i < xs.Length; i++)\n    {\n        Log(xs[i]);\n        s += xs[i];\n    }\n    return s;\n}",
            DiffCategory.Mid),

        new("31","MID try 块内插入",
            "public void Do()\n{\n    try\n    {\n        A();\n    }\n    catch { }\n}",
            "public void Do()\n{\n    try\n    {\n        A();\n        B();\n    }\n    catch { }\n}",
            DiffCategory.Mid),

        new("32","MID catch 内插入",
            "public void Do()\n{\n    try { }\n    catch (Exception e)\n    {\n        Log(e);\n    }\n}",
            "public void Do()\n{\n    try { }\n    catch (Exception e)\n    {\n        Log(e);\n        Report(e);\n    }\n}",
            DiffCategory.Mid),

        new("33","MID finally 内插入",
            "public void Do()\n{\n    try { }\n    finally\n    {\n        Cleanup();\n    }\n}",
            "public void Do()\n{\n    try { }\n    finally\n    {\n        Cleanup();\n        Audit();\n    }\n}",
            DiffCategory.Mid),

        new("34","MID 链式调用间插入(标记型)",
            "public string Build()\n{\n    GetPrefix();\n    GetSuffix();\n}",
            "public string Build()\n{\n    GetPrefix();\n    MarkMid();\n    GetSuffix();\n}",
            DiffCategory.Mid),

        new("35","MID switch 分支内插入",
            "public void M(int k)\n{\n    switch (k)\n    {\n        case 1:\n            A();\n            break;\n    }\n}",
            "public void M(int k)\n{\n    switch (k)\n    {\n        case 1:\n            A();\n            B();\n            break;\n    }\n}",
            DiffCategory.Mid),

        new("36","MID 带参数调用插入",
            "public void Step()\n{\n    Prep();\n    Done();\n}",
            "public void Step()\n{\n    Prep();\n    LogValue(Count);\n    Done();\n}",
            DiffCategory.Mid),

        new("37","MID box 值类型参数插入",
            "public void Go()\n{\n    Fetch();\n    Done();\n}",
            "public void Go()\n{\n    Fetch();\n    LogBoxed(GetVal());\n    Done();\n}",
            DiffCategory.Mid),

        new("38","MID 同方法多处插入",
            "public void Flow()\n{\n    Alpha();\n    Beta();\n    Gamma();\n}",
            "public void Flow()\n{\n    Alpha();\n    X();\n    Beta();\n    Y();\n    Gamma();\n}",
            DiffCategory.Mid),

        // ===== L4 非IL可映射（39-42）=====
        new("39","NIL csproj 加 PackageReference",
            "<Project></Project>",
            "<Project><PackageReference Include=\"X\" /></Project>",
            DiffCategory.Nil, FileName: "Sample.csproj"),

        new("40","NIL 改 TargetFramework",
            "<TargetFramework>net8.0</TargetFramework>",
            "<TargetFramework>net9.0</TargetFramework>",
            DiffCategory.Nil, FileName: "Sample.csproj"),

        new("41","NIL 加 #if 预处理指令",
            "public void M()\n{\n    DoThing();\n}",
            "public void M()\n{\n    DoThing();\n#if DEBUG\n    Log();\n#endif\n}",
            DiffCategory.Nil),

        new("42","NIL 加 using",
            "using System;",
            "using System;\nusing System.Linq;",
            DiffCategory.Nil),

        // ===== L5 复杂交叉（43-50）=====
        new("43","MIX 单文件多类多改动",
            "public class P\n{\n    public int A() => 1;\n}\npublic class Q\n{\n    public string B() => \"b\";\n}",
            "public class P\n{\n    public int A() => 2;\n    public int C() => 3;\n}\npublic class Q\n{\n    public string B() => \"B\";\n}",
            DiffCategory.Body),

        new("44","MIX 同方法既改体又改签名(签名优先)",
            "public int Compute(int x) => x;",
            "public long Compute(int x, int y) => x + y;",
            DiffCategory.Sig),

        new("45","MIX 新增成员引用签名变更方法",
            "public class Svc\n{\n    public void Send(string m) { }\n}",
            "public class Svc\n{\n    public void Send(string m, int prio) { }\n    public void Quick(string m) => Send(m, 0);\n}",
            DiffCategory.Sig),

        new("46","MIX 中间插入+签名变更",
            "public class Eng\n{\n    public void Update(int dt) { A(); C(); }\n    public int Tick() => 1;\n}",
            "public class Eng\n{\n    public void Update(int dt) { A(); B(); C(); }\n    public long Tick() => 1L;\n}",
            DiffCategory.Sig),

        new("47","MIX 多文件跨改动(B文件)",
            "public class B\n{\n    public int Twice() => new A().Id() * 2;\n}",
            "public class B\n{\n    public int Twice() => new A().Id() * 3;\n    public int Thrice() => new A().Id() * 4;\n}",
            DiffCategory.Body),

        new("48","EDGE readonly 字段初始化器",
            "public readonly int Limit = 10;",
            "public readonly int Limit = 20;",
            DiffCategory.Init),

        new("49","EDGE const+autoprop 混合(const)",
            "public const int PageSize = 50;\npublic string Mode { get; set; } = \"list\";",
            "public const int PageSize = 100;\npublic string Mode { get; set; } = \"grid\";",
            DiffCategory.Const),

        new("50","EDGE 删除+新增混合",
            "public class Legacy\n{\n    public void Run() { }\n    public void Debug() { }\n}",
            "public class Legacy\n{\n    public void Run() { }\n}",
            DiffCategory.Remove),
    };
}
