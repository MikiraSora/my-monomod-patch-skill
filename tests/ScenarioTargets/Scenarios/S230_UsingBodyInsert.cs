using System.Text;

namespace MonoModTestTargets;

public class S230_UsingBodyInsert : System.IDisposable
{
    public StringBuilder Log { get; } = new();
    public bool Disposed { get; set; }

    public void Inner() => Log.Append("inner;");
    public void After() => Log.Append("after;");

    public void Run()
    {
        using (this)
        {
            Inner();
            After();
        }
    }

    public void Dispose() => Disposed = true;
}