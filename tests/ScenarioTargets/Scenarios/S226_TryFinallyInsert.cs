using System.Text;

namespace MonoModTestTargets;

public class S226_TryFinallyInsert
{
    public StringBuilder Log { get; } = new();
    public bool CleanupDone { get; set; }

    public void Work() => Log.Append("work;");
    public void Cleanup() => CleanupDone = true;

    public void Run()
    {
        try
        {
            Work();
        }
        finally
        {
            Cleanup();
        }
    }
}