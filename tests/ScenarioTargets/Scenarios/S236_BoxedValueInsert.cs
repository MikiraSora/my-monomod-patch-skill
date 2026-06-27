using System.Text;

namespace MonoModTestTargets;

public class S236_BoxedValueInsert
{
    public StringBuilder Log { get; } = new();
    public int BoxedValue { get; set; } = 77;

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        First();
        Last();
    }
}