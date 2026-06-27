using System.Text;

namespace MonoModTestTargets;

public class S245_RefStructInsert
{
    public StringBuilder Log { get; } = new();

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public int SumSpan(System.Span<int> span)
    {
        int sum = 0;
        foreach (var v in span) sum += v;
        return sum;
    }

    public void Run()
    {
        First();
        int[] arr = { 1, 2, 3 };
        var sum = SumSpan(arr);
        Log.Append($"sum={sum};");
        Last();
    }
}