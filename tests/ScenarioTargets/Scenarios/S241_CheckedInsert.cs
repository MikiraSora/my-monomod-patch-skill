using System.Text;

namespace MonoModTestTargets;

public class S241_CheckedInsert
{
    public StringBuilder Log { get; } = new();

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        checked
        {
            int a = 100;
            int b = 200;
            int sum = a + b;
            First();
            Log.Append($"sum={sum};");
            Last();
        }
    }
}