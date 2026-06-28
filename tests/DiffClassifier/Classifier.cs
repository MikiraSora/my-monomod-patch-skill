using System.Text.RegularExpressions;

namespace MonoModDiffClassifier;

/// <summary>
/// diff 改动分类，对应 git-diff-workflow.md 映射表的每一行。
/// </summary>
public enum DiffCategory
{
    Unknown,
    Body,        // 改方法体 → orig_ wrapper
    New,         // 新增成员/类型 → patch_ 声明
    Acc,         // 改可访问性 → [MonoModPublic]
    Const,       // 改 const 值 → 重新声明 const
    Init,        // 改自动属性/readonly 字段初始化器 → 转 ctor patch 赋值
    Remove,      // 删成员 → [MonoModRemove]（破坏性）
    Sig,         // 签名变更 → 硬限制，跳过+报告+提醒
    Mid,         // 方法体中间插入 → 待决策（IL插入/复制整段）
    Nil,         // 非 IL 可映射（csproj/预处理指令/using）→ 跳过+报告
}

/// <summary>
/// 对一处 diff 改动的判定结果。
/// </summary>
public record Classification(
    DiffCategory Category,
    string Member,
    string Reason,
    double Confidence,
    string FilePath)
{
    public bool NeedsUserDecision => Category == DiffCategory.Mid;
    public bool IsSkipped => Category is DiffCategory.Sig or DiffCategory.Nil;
}

/// <summary>
/// 从 git diff 文本判定每处改动分类的启发式分类器。
/// 输入是 <c>git diff base..head</c> 的完整输出文本。
///
/// 实现思路（行级 + 括号层级跟踪，不依赖 Roslyn）：
/// - 按 <c>diff --git</c> 头切分文件；.csproj 等非 .cs 文件整体判 Nil。
/// - 逐 hunk 解析 +/- 行；用括号深度判断某行属于“签名区”还是“方法体区”。
/// - 同名成员的签名区差异（参数/返回类型/修饰符）→ Sig；
///   方法体区纯插入 → Mid；方法体区替换 → Body；
///   单边出现 → New / Remove；const 值变 → Const；可访问性变 → Acc。
/// - 边界情况给置信度 &lt; 1.0，低置信标 NeedsReview。
/// </summary>
public static class Classifier
{
    /// <summary>同一成员名在 +/- 两侧都出现，但签名 token 不同 → 判签名变更。</summary>
    private static readonly Regex MemberDeclPattern = new(
        @"^\s*(?:public|private|protected|internal|static|readonly|const|sealed|abstract|virtual|override|new|partial|async|extern|unsafe|volatile|in|out|ref|\s)*\s*(?:[\w.<>\[\],\?]+\s+)*(\w+)\s*[<(]",
        RegexOptions.Compiled);

    private static readonly Regex ConstPattern = new(
        @"const\s+\w+\s+(\w+)\s*=", RegexOptions.Compiled);

    private static readonly Regex UsingPattern = new(@"^\s*using\s", RegexOptions.Compiled);
    private static readonly Regex IfDefPattern = new(@"^\s*#\s*(if|elif|else|endif|define|undef|region|endregion)", RegexOptions.Compiled);

    public static IReadOnlyList<Classification> Classify(string diffText)
    {
        var results = new List<Classification>();
        if (string.IsNullOrWhiteSpace(diffText))
            return results;

        var fileBlocks = SplitByFiles(diffText);
        foreach (var block in fileBlocks)
        {
            results.AddRange(ClassifyFile(block));
        }
        return results;
    }

    private record FileBlock(string Path, List<string> Lines);

    private static List<FileBlock> SplitByFiles(string diffText)
    {
        var blocks = new List<FileBlock>();
        var lines = diffText.Replace("\r\n", "\n").Split('\n');
        FileBlock? cur = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("diff --git "))
            {
                if (cur is not null) blocks.Add(cur);
                var m = Regex.Match(line, @"diff --git a/(\S+) b/(\S+)");
                var p = m.Success ? m.Groups[2].Value : line;
                cur = new FileBlock(p, new List<string>());
            }
            else if (cur is not null)
            {
                cur.Lines.Add(line);
            }
        }
        if (cur is not null) blocks.Add(cur);
        return blocks;
    }

    private static IEnumerable<Classification> ClassifyFile(FileBlock block)
    {
        var path = block.Path;

        // 非 .cs 文件（csproj / props / targets / json / xml …）整体 Nil。
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            yield return new Classification(DiffCategory.Nil, path,
                "non-source file (build wiring / config), no IL equivalent", 1.0, path);
            yield break;
        }

        // 收集原始 hunk 行（保留 +/- 标记与顺序），用于带上下文的成员归属。
        var rawLines = new List<(char Mark, string Text)>();
        var added = new List<string>();
        var removed = new List<string>();
        foreach (var line in block.Lines)
        {
            if (line.StartsWith("+++") || line.StartsWith("---")) continue;
            if (line.StartsWith("+")) { rawLines.Add(('+', line[1..])); added.Add(line[1..]); }
            else if (line.StartsWith("-")) { rawLines.Add(('-', line[1..])); removed.Add(line[1..]); }
            else if (line.StartsWith(" ")) rawLines.Add((' ', line[1..]));
        }

        // 预处理指令 / using 整体 Nil。
        if (HasOnly(added, removed, UsingPattern) || HasAny(added, removed, IfDefPattern))
        {
            yield return new Classification(DiffCategory.Nil, path,
                "preprocessor directive or namespace import, no IL equivalent", 1.0, path);
            yield break;
        }

        // 按声明成员归类：用上下文行跟踪括号深度，把 +/- 体行归属到所属成员。
        // added 侧与 removed 侧分别归属，再按成员名合并。
        var addedByMember = AssignToMembers(rawLines, '+');
        var removedByMember = AssignToMembers(rawLines, '-');

        var allMembers = addedByMember.Keys.Concat(removedByMember.Keys).Distinct().ToList();
        if (allMembers.Count == 0)
        {
            if (added.Count > 0 || removed.Count > 0)
                yield return new Classification(DiffCategory.Unknown, path,
                    "unrecognized change shape, needs review", 0.2, path);
            yield break;
        }

        foreach (var member in allMembers)
        {
            var addInfo = addedByMember.GetValueOrDefault(member) ?? MemberLines.Empty;
            var remInfo = removedByMember.GetValueOrDefault(member) ?? MemberLines.Empty;
            var c = ClassifyMember(member, addInfo, remInfo, path);
            if (c is not null) yield return c;
        }
    }

    /// <summary>某成员在某侧（+/-）被归属的行集合，及纯插入时的位置信息。</summary>
    private sealed record MemberLines(List<string> Lines, bool InsertAtEnd)
    {
        public static MemberLines Empty => new(new List<string>(), false);
    }

    /// <summary>
    /// 遍历原始 hunk 行，跟踪括号深度与“当前所在成员声明”，
    /// 把指定标记（'+' 或 '-'）的行归属到当前成员名下。
    /// 上下文行与目标标记行都参与括号深度推进（context 和 +/- 在同一物理位置），
    /// 这样方法签名（常为上下文行）也能成为归属锚点。
    /// 对 target='+' 侧，额外记录每条 + 行之后是否还有 context 体行：
    /// 若所有 + 行都在方法体末尾（之后只有 } ）→ InsertAtEnd=true（末尾追加，orig_ 可处理）。
    /// </summary>
    private static Dictionary<string, MemberLines> AssignToMembers(List<(char Mark, string Text)> raw, char target)
    {
        var result = new Dictionary<string, MemberLines>();
        // 记录每个成员是否存在“+ 行之后还有 context 体行”（即中间插入）。
        var hasTrailingContext = new Dictionary<string, bool>();
        // 记录每个成员是否有至少一条 + 行。
        var hasInsertion = new Dictionary<string, bool>();

        string currentMember = "<file>";   // 顶层（未进入任何成员）的改动归属到伪成员 <file>。
        int depth = 0;                      // 当前大括号深度（相对于文件顶层）。

        MemberLines Ensure(string m)
        {
            if (!result.TryGetValue(m, out var v))
            {
                v = new MemberLines(new List<string>(), true);
                result[m] = v;
                hasTrailingContext[m] = false;
                hasInsertion[m] = false;
            }
            return v;
        }

        foreach (var (mark, text) in raw)
        {
            bool contributes = mark == ' ' || mark == target;
            var eff = contributes ? text : "";

            if (contributes && IsDeclaration(text))
            {
                var name = ExtractMemberName(text);
                if (name is not null)
                    currentMember = name;
            }

            // 归属：目标标记行归到当前成员。
            if (mark == target)
            {
                var info = Ensure(currentMember);
                info.Lines.Add(text);
                hasInsertion[currentMember] = true;
            }

            // 推进括号深度（仅 contributes 行）。
            foreach (var ch in eff)
            {
                if (ch == '{') { depth++; }
                else if (ch == '}') { depth--; }
            }

            // 对 target='+' 侧：若当前成员已有 + 行，且本 context 行处理后 depth 仍 > 0
            // （即还在方法体内、未到方法体闭合的 }），说明插入之后还有方法体内容
            // （含嵌套块的闭合 }） → 中间插入（非末尾追加）。
            // 方法体最外层末尾的 } 会让 depth 回到 0，不算 trailing → 末尾追加。
            if (target == '+' && mark == ' ' && text.Trim().Length > 0 && depth > 0)
            {
                if (hasInsertion.GetValueOrDefault(currentMember, false))
                    hasTrailingContext[currentMember] = true;
            }

            // 表达式体成员（无 { }，以 ; 结尾的声明行）：声明完即回到文件层。
            if (contributes && !text.Contains('{') && (text.Contains(';') || text.Contains("=>")) && depth == 0)
            {
                if (IsDeclaration(text)) currentMember = "<file>";
            }

            // 深度回到 0：当前成员体结束，回归文件层。
            if (depth <= 0 && mark == ' ' && text.TrimStart().StartsWith("}"))
                currentMember = "<file>";
        }

        // 把 InsertAtEnd 标志写回每个成员（中间插入 → InsertAtEnd=false）。
        foreach (var kv in result)
        {
            var atEnd = !hasTrailingContext.GetValueOrDefault(kv.Key, false);
            result[kv.Key] = kv.Value with { InsertAtEnd = atEnd };
        }

        return result;
    }

    private static Classification? ClassifyMember(string member, MemberLines added, MemberLines removed, string path)
    {
        var addLines = added.Lines;
        var remLines = removed.Lines;

        // <file> 伪成员：顶层零散改动（如新增 using 已被前置 Nil 拦截；这里多为新增类型声明行）。
        // 仅当其中确实含声明时才判定，否则跳过避免噪声。
        if (member == "<file>")
        {
            var addDecl = addLines.FirstOrDefault(IsDeclaration);
            if (addDecl is not null && remLines.All(l => !IsDeclaration(l)))
                return new Classification(DiffCategory.New, ExtractMemberName(addDecl) ?? member,
                    "new top-level type/member declared", 0.85, path);
            return null;
        }

        // 取两侧的声明行（可能为空——签名是 context 时两侧都没有声明行）。
        var addDeclLine = addLines.FirstOrDefault(IsDeclaration);
        var remDeclLine = remLines.FirstOrDefault(IsDeclaration);

        // ---- 两侧都有声明行：声明本身被改 → 比较签名/const/acc/init ----
        if (addDeclLine is not null && remDeclLine is not null)
        {
            var addConst = ConstPattern.Match(addDeclLine);
            var remConst = ConstPattern.Match(remDeclLine);
            if (addConst.Success && remConst.Success && addConst.Groups[1].Value == remConst.Groups[1].Value)
                return new Classification(DiffCategory.Const, member, "const value changed", 0.95, path);

            if (OnlyAccessibilityDiffers(addDeclLine, remDeclLine))
                return new Classification(DiffCategory.Acc, member, "accessibility modifier changed", 0.85, path);

            if (IsInitLikeChange(addDeclLine, remDeclLine, out var initReason))
                return new Classification(DiffCategory.Init, member, initReason, 0.8, path);

            if (SignatureDiffers(addDeclLine, remDeclLine))
                return new Classification(DiffCategory.Sig, member,
                    DescribeSigDiff(addDeclLine, remDeclLine), 0.8, path);

            // 声明相同，差异在体。
            return ClassifyBodyChange(member, addLines, remLines, added.InsertAtEnd, path, addDeclLine, remDeclLine);
        }

        // ---- 仅 added 有声明行（removed 无声明）→ New ----
        if (addDeclLine is not null && remDeclLine is null && remLines.Count == 0)
            return new Classification(DiffCategory.New, member, "new member/type declared", 0.9, path);

        // ---- 仅 removed 有声明行（added 无声明）→ Remove ----
        if (remDeclLine is not null && addDeclLine is null && addLines.Count == 0)
            return new Classification(DiffCategory.Remove, member, "member removed", 0.85, path);

        // ---- 两侧都无声明行：纯体变更（签名是 context 未变）----
        if (addDeclLine is null && remDeclLine is null)
            return ClassifyBodyChange(member, addLines, remLines, added.InsertAtEnd, path, null, null);

        // 混合（一侧有声明一侧无）：保守判为 New/Remove 或 Unknown。
        if (addDeclLine is not null && remLines.Count == 0)
            return new Classification(DiffCategory.New, member, "new member/type declared", 0.85, path);
        if (remDeclLine is not null && addLines.Count == 0)
            return new Classification(DiffCategory.Remove, member, "member removed", 0.8, path);

        return new Classification(DiffCategory.Unknown, member, "mixed change shape, needs review", 0.3, path);
    }

    /// <summary>
    /// 判定方法体变更：
    /// - 纯插入且在方法体中间（+ 行之后还有 context 体行）→ Mid（orig_ 无法表达）
    /// - 纯插入但在方法体末尾（之后只有 }）→ Body（orig_ wrapper 的 after 阶段可追加）
    /// - 行内插入（added 行包含 removed 行内容）→ Mid
    /// - 其余有 removed 体行 → Body（体逻辑替换）
    /// </summary>
    private static Classification ClassifyBodyChange(string member, List<string> added, List<string> removed,
        bool insertAtEnd, string path, string? addDeclLine, string? remDeclLine)
    {
        // 去掉声明行，剩下的都是体行。
        var bodyAdded = added.Where(l => l != addDeclLine).ToList();
        var bodyRemoved = removed.Where(l => l != remDeclLine).ToList();

        // 纯插入（无 removed 体行）。
        if (bodyRemoved.Count == 0 && bodyAdded.Count > 0)
        {
            // 末尾追加：orig_ wrapper 的 after 阶段即可处理 → Body。
            if (insertAtEnd)
                return new Classification(DiffCategory.Body, member,
                    "body changed (append at end, orig_ after handles it)", 0.8, path);
            // 中间插入：orig_ 只能 before/after 整个方法，插不进两条语句之间 → Mid。
            return new Classification(DiffCategory.Mid, member,
                "code inserted inside method body (orig_ cannot express)", 0.7, path);
        }

        // 行内插入检测：当 added 体行“包含”对应 removed 体行（旧行内容是新行子串），
        // 说明是在原有语句行内插入了新代码，而非替换整条语句 → Mid。
        if (IsInlineInsertion(bodyAdded, bodyRemoved))
            return new Classification(DiffCategory.Mid, member,
                "code inserted inside an existing statement (orig_ cannot express)", 0.65, path);

        // 有 removed 体行且非行内插入 → 体逻辑替换。
        return new Classification(DiffCategory.Body, member,
            "method body logic changed", 0.85, path);
    }

    /// <summary>
    /// 启发式：added 体行集合是否“包含”removed 体行集合的内容（行内插入）。
    /// 把每行归一化（去空白）后，检查所有 removed 体行内容是否作为子串出现在
    /// added 体行的拼接里；且 added 比 removed 多出实质内容。
    /// </summary>
    private static bool IsInlineInsertion(List<string> bodyAdded, List<string> bodyRemoved)
    {
        if (bodyAdded.Count == 0 || bodyRemoved.Count == 0) return false;
        var addJoined = string.Join(" ", bodyAdded.Select(Normalize)).Trim();
        var remJoined = string.Join(" ", bodyRemoved.Select(Normalize)).Trim();
        if (addJoined.Length <= remJoined.Length) return false;
        // 旧行的每个非空 token 序列应在新行里出现。
        return addJoined.Contains(remJoined);
    }

    private static string Normalize(string line) => Regex.Replace(line.Trim(), @"\s+", " ");

    private static string? ExtractMemberName(string line)
    {
        var t = line.TrimStart();
        // 类型声明: class/struct/interface/enum/record Name
        var typeDecl = Regex.Match(t, @"\b(class|struct|interface|enum|record)\s+(\w+)");
        if (typeDecl.Success) return typeDecl.Groups[2].Value;

        // 方法/构造/泛型：Name(  或  Name<
        var m = MemberDeclPattern.Match(line);
        if (m.Success) return m.Groups[1].Value;

        // 字段/属性/const: [modifiers] Type Name [= ...]  或  Type Name { get; set; }
        // 要求行以声明性修饰符/类型开头，排除裸语句调用。
        if (!LooksLikeDeclarationStart(t)) return null;

        // 属性: ... Name { get; ...
        var prop = Regex.Match(t, @"(\w+)\s*\{\s*get");
        if (prop.Success) return prop.Groups[1].Value;

        // const/字段: ... Type Name = ...  或  ... Type Name;
        var field = Regex.Match(t, @"(\w+)\s*(?:=|;)");
        if (field.Success) return field.Groups[1].Value;

        return null;
    }


    /// <summary>行是否以声明性关键字或“类型 标识符”开头（区别于方法体内的裸语句）。</summary>
    private static bool LooksLikeDeclarationStart(string t)
    {
        if (Regex.IsMatch(t, @"^(public|private|protected|internal|static|readonly|const|sealed|abstract|virtual|override|new|partial|async|extern|unsafe|class|struct|interface|enum|record|namespace)\b"))
            return true;
        return false;
    }

    private static bool IsDeclaration(string line)
    {
        var t = line.TrimStart();
        if (t.Length == 0) return false;
        // 排除明显语句：return / 赋值 / 调用 / using / if / for 等。
        if (t.StartsWith("return ") || t == "return" || t.StartsWith("return;")) return false;
        if (Regex.IsMatch(t, @"^(if|for|foreach|while|switch|using|lock|try|catch|finally|throw|else|do)\b")) return false;
        if (t.StartsWith("//")) return false;
        // 声明需以声明性关键字开头（修饰符/类型声明关键字），
        // 或为方法/属性声明模式。裸语句调用（如 B();）不以这些开头 → 不是声明。
        if (LooksLikeDeclarationStart(t)) return true;
        // 方法声明也可能无修饰符（如接口方法、表达式体）但测试用例均有修饰符，此处从严。
        return MemberDeclPattern.IsMatch(line) && !Regex.IsMatch(t, @"^\w+\s*\(.*\)\s*;");
    }


    private static bool OnlyAccessibilityDiffers(string a, string b)
    {
        var acc = new HashSet<string> { "public", "private", "protected", "internal" };
        var ta = Tokenize(a);
        var tb = Tokenize(b);
        var aAcc = ta.Where(acc.Contains).OrderBy(x => x).ToList();
        var bAcc = tb.Where(acc.Contains).OrderBy(x => x).ToList();
        if (aAcc.SequenceEqual(bAcc)) return false;
        // 其余 token（除可访问性）应一致。
        var aRest = ta.Where(t => !acc.Contains(t)).ToList();
        var bRest = tb.Where(t => !acc.Contains(t)).ToList();
        return aRest.SequenceEqual(bRest);
    }

    private static bool IsInitLikeChange(string a, string b, out string reason)
    {
        reason = "";
        var autoProp = new Regex(@"(\w+)\s*\{\s*get;\s*set;\s*}\s*=\s*(.+?);");
        var ma = autoProp.Match(a); var mb = autoProp.Match(b);
        if (ma.Success && mb.Success && ma.Groups[1].Value == mb.Groups[1].Value
            && ma.Groups[2].Value != mb.Groups[2].Value)
        { reason = "auto-property initializer changed (not copied by MonoMod)"; return true; }

        var roField = new Regex(@"readonly\s+\w+\s+(\w+)\s*=\s*(.+?);");
        var ra = roField.Match(a); var rb = roField.Match(b);
        if (ra.Success && rb.Success && ra.Groups[1].Value == rb.Groups[1].Value
            && ra.Groups[2].Value != rb.Groups[2].Value)
        { reason = "readonly field initializer changed (not copied by MonoMod)"; return true; }
        return false;
    }

    private static bool SignatureDiffers(string a, string b)
    {
        // 比较去掉可访问性修饰符后的签名 token。
        var acc = new HashSet<string> { "public", "private", "protected", "internal" };
        var ta = Tokenize(a).Where(t => !acc.Contains(t)).ToList();
        var tb = Tokenize(b).Where(t => !acc.Contains(t)).ToList();
        return !ta.SequenceEqual(tb);
    }

    private static string DescribeSigDiff(string a, string b)
    {
        var pa = ExtractParamList(a);
        var pb = ExtractParamList(b);
        if (pa != pb) return $"parameter list changed: ({pa}) -> ({pb})";
        var ra = ExtractReturnType(a);
        var rb = ExtractReturnType(b);
        if (ra != rb) return $"return type changed: {ra} -> {rb}";
        return "signature changed (modifier/generics/static)";
    }

    private static string ExtractParamList(string decl)
    {
        var m = Regex.Match(decl, @"\(([^)]*)\)");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static string ExtractReturnType(string decl)
    {
        var t = decl.TrimStart();
        // 方法: modifiers... ReturnType Name(...)
        var m = Regex.Match(t, @"(?:public|private|protected|internal|static|readonly|const|sealed|abstract|virtual|override|new|partial|async|extern|unsafe|\s)*\s*([\w<>\[\],\?]+)\s+\w+\s*\(");
        return m.Success ? m.Groups[1].Value : "";
    }

    private static List<string> Tokenize(string line)
    {
        var t = line.Trim();
        // 保留括号内参数与类型 token，去掉值。
        t = Regex.Replace(t, @"=\s*[^;]+", "=");   // 抹掉初始化值
        var tokens = new List<string>();
        foreach (Match raw in Regex.Matches(t, @"\w+|[<>\[\],\?\(\)\{\};]"))
            tokens.Add(raw.Value);
        return tokens;
    }

    private static bool HasOnly(List<string> added, List<string> removed, Regex pat)
        => (added.Concat(removed)).Where(l => l.Trim().Length > 0).All(l => pat.IsMatch(l) || l.Trim().StartsWith("//"))
           && (added.Concat(removed)).Any(l => pat.IsMatch(l));

    private static bool HasAny(List<string> added, List<string> removed, Regex pat)
        => (added.Concat(removed)).Any(l => pat.IsMatch(l));
}
