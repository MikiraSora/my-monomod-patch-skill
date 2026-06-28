using MonoModDiffClassifier;

namespace MonoModDiffClassifier.Tests;

internal static class Program
{
    private static int Main()
    {
        var cases = Cases.All();
        var sb = new System.Text.StringBuilder();
        int pass = 0, fail = 0;

        foreach (var c in cases)
        {
            var diff = DiffBuilder.Build(c.FileName, c.Base, c.Head);
            var classifications = Classifier.Classify(diff);
            var gotCategories = classifications.Select(x => x.Category).Distinct().ToList();

            // 期望：期望分类出现在判定集合中（一例可能产生多个判定，只要期望那个在即可）。
            bool ok = gotCategories.Contains(c.Expected);
            if (ok) pass++; else fail++;

            var gotStr = string.Join(",", gotCategories);
            Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {c.Id} {c.Name} :: expected={c.Expected} got=[{gotStr}]");
            if (!ok)
            {
                sb.AppendLine($"## {c.Id} {c.Name}");
                sb.AppendLine($"- expected: {c.Expected}");
                sb.AppendLine($"- got: [{gotStr}]");
                sb.AppendLine("- classifications:");
                foreach (var cl in classifications)
                    sb.AppendLine($"    - {cl.Category} {cl.Member} :: {cl.Reason} (conf={cl.Confidence})");
                sb.AppendLine("- diff:");
                sb.AppendLine("```diff");
                sb.AppendLine(diff);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        File.WriteAllText("classifier-test-report.md", sb.ToString(), new System.Text.UTF8Encoding(false));
        Console.WriteLine($"PASS={pass} FAIL={fail} TOTAL={cases.Count}");
        return fail == 0 ? 0 : 1;
    }
}
