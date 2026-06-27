using System.Text;

namespace MonoModTestTargets;

public enum S233_Level { Low, Medium, High }

public class S233_EnumArgInsert
{
    public StringBuilder Log { get; } = new();
    public S233_Level Current { get; set; } = S233_Level.Medium;

    public void Start() { }
    public void Stop() { }

    public void Run()
    {
        Start();
        Stop();
    }
}