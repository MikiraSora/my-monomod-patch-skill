using System.Text;

namespace MonoModTestTargets;

public class S227_MultiInsertSameMethod
{
    public StringBuilder Log { get; } = new();

    public void Alpha() => Log.Append("A;");
    public void Beta() => Log.Append("B;");
    public void Gamma() => Log.Append("G;");

    public void Run()
    {
        Alpha();
        Beta();
        Gamma();
    }
}