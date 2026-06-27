using System.Text;

namespace MonoModTestTargets;

public class S231_BeforeRetInsert
{
    public StringBuilder Log { get; } = new();

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        First();
    }
}