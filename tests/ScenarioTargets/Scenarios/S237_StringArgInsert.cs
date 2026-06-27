using System.Text;

namespace MonoModTestTargets;

public class S237_StringArgInsert
{
    public StringBuilder Log { get; } = new();

    public void Alpha() => Log.Append("A;");
    public void Omega() => Log.Append("O;");

    public void Run()
    {
        Alpha();
        Omega();
    }
}