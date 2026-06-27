# Git Diff → Patch Test Cases (50)

规格化测试集，验证 `monomod-patch-dll/references/git-diff-workflow.md` 的 diff 分类与映射逻辑。

每例给出 **base**（commit A）与 **head**（commit B）的源码片段、**分类**（映射表行）、**期望处置**（生成的 patch 模式 / 跳过 / 待决策 / 非IL）。难度按 L1→L5 递进。

分类图例：
- `BODY` = 改方法体 → `orig_` wrapper
- `NEW` = 新增成员/类型 → `patch_` 声明
- `ACC` = 改可访问性 → `[MonoModPublic]`
- `CONST` = 改 const 值 → 重新声明 const
- `INIT` = 改自动属性初始化器 → 转 ctor patch 赋值
- `REMOVE` = 删成员 → `[MonoModRemove]`（破坏性）
- `SIG` = 签名变更 → **硬限制，跳过 + 报告 + 提醒用户**
- `MID` = 方法体中间插入 → **待决策（IL 插入 / 复制整段），报告逐项列出并停下**
- `NIL` = 非 IL 可映射（csproj / 预处理指令）→ 跳过 + 报告

---

## L1 — 单一、原子改动（1-10）

### 01 BODY — 方法体逻辑变更（返回值加工）
```csharp
// base
public string Greet(string name) => "hi " + name;
// head
public string Greet(string name) => "hello " + name.ToUpperInvariant();
```
- 分类：BODY
- 期望：`orig_Greet` wrapper，新实现返回 `"hello " + orig_Greet(name).ToUpperInvariant()`

### 02 BODY — void 方法副作用变更
```csharp
// base
public void Log(string m) { Console.WriteLine(m); }
// head
public void Log(string m) { Console.WriteLine("[DBG] " + m); }
```
- 分类：BODY
- 期望：`orig_Log` wrapper，先/后加工输出

### 03 BODY — 静态方法体变更
```csharp
// base
public static int Id(int x) => x;
// head
public static int Id(int x) => x * 2;
```
- 分类：BODY
- 期望：`orig_Id` wrapper（静态），`return orig_Id(x) * 2`

### 04 NEW — 新增实例方法
```csharp
// base
public class Foo { public int A() => 1; }
// head
public class Foo { public int A() => 1; public int B() => 2; }
```
- 分类：NEW
- 期望：`patch_Foo` 声明新方法 `B()`，被复制进目标

### 05 NEW — 新增静态字段
```csharp
// base
public class Counter { public int N; }
// head
public class Counter { public int N; public static int Total; }
```
- 分类：NEW
- 期望：`patch_Counter` 声明 `public static int Total`（默认 0，初始化器不复制）

### 06 NEW — 新增独立类型
```csharp
// base
namespace App;
// head
namespace App;
public class Logger { public void Write(string s){} }
```
- 分类：NEW
- 期望：patch 程序集直接含 `Logger` 新类，原样进目标

### 07 ACC — private → public
```csharp
// base
private void Reset() { }
// head
public void Reset() { }
```
- 分类：ACC
- 期望：`[MonoModPublic]` 作用于 `Reset`

### 08 CONST — 改 const 值
```csharp
// base
public const int MaxRetry = 3;
// head
public const int MaxRetry = 5;
```
- 分类：CONST
- 期望：`patch_` 重新声明 `public const int MaxRetry = 5`（元数据覆盖）

### 09 BODY — 构造函数体变更
```csharp
// base
public Widget(string n){ Name = n; }
// head
public Widget(string n){ Name = n; Tag = "default"; }
```
- 分类：BODY（构造函数）
- 期望：`orig_ctor` + `[MonoModConstructor]`，`orig_ctor(n); Tag = "default";`

### 10 NEW — 新增属性（自动属性）
```csharp
// base
public class Node { public int Value; }
// head
public class Node { public int Value; public string Label { get; set; } }
```
- 分类：NEW
- 期望：`patch_Node` 声明 `Label` 自动属性（初始 null，需注意 INIT 类问题）

---

## L2 — 组合改动 / 多成员（11-20）

### 11 BODY×2 — 同一类型两个方法体变更
```csharp
// base
public int Add(int a,int b)=>a+b;
public int Sub(int a,int b)=>a-b;
// head
public int Add(int a,int b)=>a+b+1;
public int Sub(int a,int b)=>a-b-1;
```
- 分类：BODY（×2）
- 期望：同一 `patch_` 文件含两个 `orig_` wrapper

### 12 NEW+BODY — 新增方法并改另一方法体
```csharp
// base
public string Format(string s)=>s;
// head
public string Format(string s)=>s.Trim();
public string Decorate(string s)=>"["+s+"]";
```
- 分类：BODY（Format）+ NEW（Decorate）
- 期望：一个 `patch_`，`orig_Format` wrapper + 新方法 `Decorate`

### 13 INIT — 改自动属性初始化器
```csharp
// base
public string Mode { get; set; } = "auto";
// head
public string Mode { get; set; } = "manual";
```
- 分类：INIT
- 期望：不能靠重新声明初始化器（不复制）；转成 ctor patch 里 `Mode = "manual"`

### 14 NEW（带初始化器）— 新增字段并初始化
```csharp
// base
public class Cfg { }
// head
public class Cfg { public string Tag = "x"; }
```
- 分类：NEW
- 期望：声明 `Tag`，但初始化器 `"x"` 不被复制；若需初值，ctor patch 里赋值。报告中提示此陷阱

### 15 CONST×2 — 两个 const 值变更
```csharp
// base
public const int A = 1; public const int B = 2;
// head
public const int A = 10; public const int B = 20;
```
- 分类：CONST（×2）
- 期望：`patch_` 重新声明两个 const

### 16 ACC+BODY — 改可访问性并改方法体
```csharp
// base
private int Calc(int x)=>x;
// head
public int Calc(int x)=>x+1;
```
- 分类：ACC + BODY
- 期望：`[MonoModPublic]` + `orig_Calc` wrapper 返回 `orig_Calc(x)+1`

### 17 NEW — 新增构造函数
```csharp
// base
public class Box { public Box(int w){W=w;} public int W; }
// head
public class Box { public Box(int w){W=w;} public Box(int w,int h):this(w){H=h;} public int W; public int H; }
```
- 分类：NEW（ctor + 字段）
- 期望：`patch_Box` 声明新字段 `H` 与新 ctor；注意新 ctor 体引用 `H=h`，`H` 必须先被复制

### 18 REMOVE — 删除方法
```csharp
// base
public void Debug(){ /*...*/ }
// head
// (Debug removed)
```
- 分类：REMOVE
- 期望：`[MonoModRemove]` 作用于 `Debug`；报告标破坏性，提醒可能断调用方

### 19 NEW（嵌套类型）— 新增嵌套类
```csharp
// base
public class Outer { }
// head
public class Outer { public class Inner { public int V; } }
```
- 分类：NEW
- 期望：patch 程序集新增嵌套 `Inner`；确认嵌套类型映射方式（`patch_Outer` 内声明或独立声明）

### 20 BODY — 泛型方法体变更
```csharp
// base
public T Pick<T>(T a, T b)=>a;
// head
public T Pick<T>(T a, T b)=>b;
```
- 分类：BODY（泛型）
- 期望：`orig_Pick<T>` wrapper，签名含泛型参数，返回 `orig_Pick(a,b)` 改为选 b

---

## L3 — 签名变更硬限制（21-28）

> 这些都必须：**跳过 + 写入报告 + 提醒用户决定**，不生成任何 patch、不兜底。

### 21 SIG — 给方法加参数
```csharp
// base
public void TakeDamage(int amount){ }
// head
public void TakeDamage(int amount, bool ignoreArmor){ }
```
- 分类：SIG
- 期望：跳过；报告列“added param `bool ignoreArmor`”；建议改用新增重载

### 22 SIG — 删除方法参数
```csharp
// base
public void Send(string msg, int prio){ }
// head
public void Send(string msg){ }
```
- 分类：SIG
- 期望：跳过；报告列“removed param `int prio`”

### 23 SIG — 改返回类型
```csharp
// base
public int Count()=>0;
// head
public long Count()=>0L;
```
- 分类：SIG
- 期望：跳过；报告列“return type int→long”

### 24 SIG — 加泛型参数
```csharp
// base
public T Make<T>()=>default;
// head
public T Make<T,U>()=>default;
```
- 分类：SIG
- 期望：跳过；报告列“added generic param U”

### 25 SIG — instance → static
```csharp
// base
public int Value()=>42;
// head
public static int Value()=>42;
```
- 分类：SIG
- 期望：跳过；报告列“instance→static”

### 26 SIG — 改参数 ref 修饰
```csharp
// base
public void Mutate(ref int x){ }
// head
public void Mutate(out int x){ x=0; }
```
- 分类：SIG
- 期望：跳过；报告列“ref→out”

### 27 SIG — 改参数 in 修饰
```csharp
// base
public void Use(in int x){ }
// head
public void Use(int x){ }
```
- 分类：SIG
- 期望：跳过；报告列“removed `in`”

### 28 SIG — 改属性类型
```csharp
// base
public int Port { get; set; }
// head
public string Port { get; set; }
```
- 分类：SIG
- 期望：跳过；报告列“property type int→string”（属性签名变更同属硬限制）

---

## L4 — 方法体中间插入（29-38）

> 这些都属于 `MID`：**报告逐项列出，停下等用户在 IL 插入 / 复制整段之间选择**，不预生成。

### 29 MID — 在两个调用之间插入日志
```csharp
// base
public void Run(){ A(); C(); }
// head
public void Run(){ A(); B(); C(); }
```
- 分类：MID
- 期望：报告列出；用户选 A（IL 插入，锚点 A() 后）或 B（复制整段，重写 Run 含 A();B();C()）

### 30 MID — 循环体内插入
```csharp
// base
public int Sum(int[] xs){ int s=0; for(int i=0;i<xs.Length;i++) s+=xs[i]; return s; }
// head
public int Sum(int[] xs){ int s=0; for(int i=0;i<xs.Length;i++){ Log(xs[i]); s+=xs[i]; } return s; }
```
- 分类：MID
- 期望：报告列出；IL 插入需在循环体 InsertBefore 锚点；复制整段需重写整个 Sum

### 31 MID — try 块内插入
```csharp
// base
public void Do(){ try{ A(); }catch{} }
// head
public void Do(){ try{ A(); B(); }catch{} }
```
- 分类：MID
- 期望：报告列出；IL 插入在 try 体内；注意 EH 区域边界，勿跨 leave

### 32 MID — catch 处理器内插入
```csharp
// base
catch(Exception e){ Log(e); }
// head
catch(Exception e){ Log(e); Report(e); }
```
- 分类：MID
- 期望：报告列出；IL 插入在 catch handler 区域内

### 33 MID — finally 块内插入
```csharp
// base
finally{ Cleanup(); }
// head
finally{ Cleanup(); Audit(); }
```
- 分类：MID
- 期望：报告列出；IL 插入在 finally handler 区域内

### 34 MID — 链式调用之间插入
```csharp
// base
public string Build()=>GetPrefix()+GetSuffix();
// head
public string Build()=>GetPrefix()+"-"+GetSuffix();
```
- 分类：MID（变体 8 标记型，stack 中性）
- 期望：报告列出；IL 插入只能插标记对（栈非空，不能插方法调用）；复制整段最简

### 35 MID — switch 分支内插入
```csharp
// base
switch(k){ case 1: A(); break; }
// head
switch(k){ case 1: A(); B(); break; }
```
- 分类：MID
- 期望：报告列出；IL 插入在 case 1 分支内锚点

### 36 MID — 带参数的方法调用插入
```csharp
// base
public void Step(){ Prep(); }
// head
public void Step(){ Prep(); LogValue(Count); }
```
- 分类：MID（变体 3/4，需注意栈序：this→arg）
- 期望：报告列出；IL 插入需 `ldarg_0` + 取参 + `callvirt`；复制整段更稳

### 37 MID — box 值类型参数插入
```csharp
// base
public void Go(){ Fetch(); }
// head
public void Go(){ Fetch(); LogBoxed(GetVal()); } // GetVal() returns int, LogBoxed(object)
```
- 分类：MID（变体 5，需 box 操作码）
- 期望：报告列出；IL 插入需显式 `box`；复制整段更稳

### 38 MID — 同方法多处插入
```csharp
// base
public void Flow(){ Alpha(); Beta(); Gamma(); }
// head
public void Flow(){ Alpha(); X(); Beta(); Y(); Gamma(); }
```
- 分类：MID（×2，变体多插入）
- 期望：报告列出两处；IL 插入两次独立锚点（Alpha 后、Beta 后）；复制整段一次重写

---

## L4 — 非 IL 可映射（39-42）

> 这些 `NIL`：跳过 + 报告，不属于 patch 范畴。

### 39 NIL — csproj 加 PackageReference
```xml
// base .csproj
// head .csproj (added <PackageReference Include="X" />)
```
- 分类：NIL
- 期望：跳过；报告“build wiring, not a patch”

### 40 NIL — 改 TargetFramework
```xml
// base: <TargetFramework>net8.0</TargetFramework>
// head: <TargetFramework>net9.0</TargetFramework>
```
- 分类：NIL
- 期望：跳过；报告“build wiring, not a patch”

### 41 NIL — 加预处理指令 #if
```csharp
// base
public void M(){ DoThing(); }
// head
public void M(){ DoThing(); #if DEBUG Log(); #endif }
```
- 分类：NIL（条件编译是编译期概念，IL 无对应）
- 期望：跳过；报告“preprocessor directive, no IL equivalent”。若 DEBUG 下确需 Log，提示转成运行时判断 patch

### 42 NIL — 改 using / 全局 using
```csharp
// base: using System;
// head: using System; using System.Linq;
```
- 分类：NIL
- 期望：跳过；报告“namespace import, no IL equivalent”

---

## L5 — 复杂 / 交叉 / 边界（43-50）

### 43 MIX — 单文件多类多改动（按源文件拆验证）
```csharp
// base: file Mixed.cs
public class P { public int A()=>1; }
public class Q { public string B()=>"b"; }
// head: file Mixed.cs
public class P { public int A()=>2; public int C()=>3; }   // BODY + NEW
public class Q { public string B()=>"B"; }                  // BODY
```
- 分类：BODY(P.A) + NEW(P.C) + BODY(Q.B)
- 期望：单个 patch 文件 `Mixed.mm.cs` 含 `patch_P`（orig_A + 新 C）与 `patch_Q`（orig_B）。验证“按源文件拆”约定

### 44 MIX — 同方法既改体又签名（签名优先，整方法跳过）
```csharp
// base
public int Compute(int x)=>x;
// head
public long Compute(int x, int y)=>x+y;
```
- 分类：SIG（含 BODY，但签名变更使整方法不可 patch）
- 期望：跳过整方法；报告列签名变更；不部分生成

### 45 MIX — 新增成员引用签名变更方法
```csharp
// base
public class Svc { public void Send(string m){} }
// head
public class Svc { public void Send(string m, int prio){} public void Quick(string m)=>Send(m,0); }
```
- 分类：SIG(Send) + NEW(Quick)
- 期望：Send 跳过（签名变更）；Quick 虽是新增可生成，但其体调用新签名 Send，目标里只有旧 Send(string) → Quick 生成后运行时会断。报告须提示此跨依赖风险

### 46 MIX — 中间插入 + 签名变更（混合，分项处置）
```csharp
// base
public class Eng { public void Update(int dt){ A(); C(); } public int Tick()=>1; }
// head
public class Eng { public void Update(int dt){ A(); B(); C(); } public long Tick()=>1L; }
```
- 分类：MID(Update) + SIG(Tick)
- 期望：Update 进“待决策”清单；Tick 跳过进签名变更清单；两者独立处置

### 47 MIX — 多文件跨改动（按源文件拆 + 跨文件依赖）
```csharp
// base: A.cs
public class A { public int Id()=>1; }
// base: B.cs
public class B { public int Twice()=>new A().Id()*2; }
// head: A.cs
public class A { public int Id()=>2; }            // BODY
// head: B.cs
public class B { public int Twice()=>new A().Id()*3; public int Thrice()=>new A().Id()*4; } // BODY + NEW
```
- 分类：BODY(A.Id) + BODY(B.Twice) + NEW(B.Thrice)
- 期望：两个 patch 文件 `A.mm.cs`、`B.mm.cs`；B 的 patch 引用目标里的 A 类型，验证跨文件引用解析

### 48 EDGE — 改 readonly 字段初始化（语义陷阱）
```csharp
// base
public readonly int Limit = 10;
// head
public readonly int Limit = 20;
```
- 分类：INIT（readonly 字段初始化器同样不被复制）
- 期望：不能靠重声明初值；需 ctor patch 里赋 `Limit = 20`（且 readonly 字段在 ctor 外赋值需 patch ctor）。报告提示陷阱

### 49 EDGE — const + auto-prop 同类型混合
```csharp
// base
public const int PageSize = 50;
public string Mode { get; set; } = "list";
// head
public const int PageSize = 100;
public string Mode { get; set; } = "grid";
```
- 分类：CONST(PageSize) + INIT(Mode)
- 期望：PageSize 用 const 重声明（可）；Mode 转 ctor patch 赋值。同类型两种不同处置，验证分类颗粒度

### 50 EDGE — 整文件新增 + 删除混合（破坏性 + 新增）
```csharp
// base: file Old.cs
public class Legacy { public void Run(){} public void Debug(){} }
// head: file Old.cs
public class Legacy { public void Run(){} }   // Debug removed
// head: file New.cs (new file)
public class Fresh { public void Go(){} }     // new type
```
- 分类：REMOVE(Legacy.Debug) + NEW(Fresh 类型)
- 期望：`Old.mm.cs` 含 `[MonoModRemove]` 于 Debug；`New.mm.cs` 含新类 Fresh。验证删除与新增同 diff 并存、按源文件拆；删除项进破坏性提示

---

## 用例与映射表覆盖核对

| 分类 | 用例编号 | 覆盖 |
|---|---|---|
| BODY | 01,02,03,09,11,20 | 实例/void/静态/构造/泛型/多方法 |
| NEW | 04,05,06,10,14,17,19 | 方法/字段/类型/属性/ctor/嵌套 |
| ACC | 07,16 | 单纯 / 与 BODY 组合 |
| CONST | 08,15,49 | 单 / 多 / 与 INIT 混合 |
| INIT | 13,48,49 | 自动属性 / readonly / 与 CONST 混合 |
| REMOVE | 18,50 | 删方法 / 删+新增混合 |
| SIG | 21-28,44,45,46 | 加/删参 / 返回类型 / 泛型 / static / ref / out / in / 属性类型 / 混合 |
| MID | 29-38 | 简单/循环/try/catch/finally/链式/switch/带参/box/多插入 |
| NIL | 39-42 | csproj / TFM / #if / using |
| MIX/EDGE | 43,45,46,47,49,50 | 按源文件拆 / 跨依赖 / 混合处置 / 语义陷阱 |

共 50 例，L1-L5 递进，覆盖 `git-diff-workflow.md` 映射表全部行与全部三类处置（生成 / 跳过 / 待决策）。
