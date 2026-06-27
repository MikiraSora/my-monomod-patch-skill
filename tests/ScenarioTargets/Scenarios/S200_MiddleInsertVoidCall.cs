using System.Text;

namespace MonoModTestTargets;

public class S200_MiddleInsertVoidCall
{
    public StringBuilder Log { get; } = new();

    public void First() => Log.Append("1");
    public void Third() => Log.Append("3");

    public void Run()
    {
        First();
        Third();
    }
}