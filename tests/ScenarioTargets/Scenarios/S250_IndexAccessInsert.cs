using System.Text;

namespace MonoModTestTargets;

public class S250_IndexAccessInsert
{
    public StringBuilder Log { get; } = new();
    public int[] Data { get; } = { 10, 20, 30 };

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        First();
        var val = Data[1];
        Log.Append($"val={val};");
        Last();
    }
}