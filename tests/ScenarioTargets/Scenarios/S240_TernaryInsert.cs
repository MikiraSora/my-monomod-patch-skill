using System.Text;

namespace MonoModTestTargets;

public class S240_TernaryInsert
{
    public StringBuilder Log { get; } = new();
    public int Base { get; set; } = 10;

    public int GetPlus() => 1;
    public int GetMinus() => -1;

    public void Run(bool flag)
    {
        Log.Append("start;");
        int delta = flag ? GetPlus() : GetMinus();
        Log.Append($"delta={delta};");
    }
}