using System.Text;

namespace MonoModTestTargets;

public class S229_LockBodyInsert
{
    private readonly object _gate = new();
    public StringBuilder Log { get; } = new();

    public void Locked() => Log.Append("locked;");
    public void Finish() => Log.Append("finish;");

    public void Run()
    {
        lock (_gate)
        {
            Locked();
            Finish();
        }
    }
}