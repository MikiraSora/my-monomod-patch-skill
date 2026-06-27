using System.Text;

namespace MonoModTestTargets;

public class S238_LocalFuncInsert
{
    public StringBuilder Log { get; } = new();

    public void Before() => Log.Append("B;");
    public void After() => Log.Append("A;");

    public void Run()
    {
        int LocalSquare(int x) => x * x;

        Before();
        var r = LocalSquare(5);
        Log.Append($"r={r};");
        After();
    }
}