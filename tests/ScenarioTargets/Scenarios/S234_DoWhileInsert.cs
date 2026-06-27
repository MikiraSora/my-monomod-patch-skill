using System.Text;

namespace MonoModTestTargets;

public class S234_DoWhileInsert
{
    public StringBuilder Log { get; } = new();
    public int Count { get; set; }

    public void Tick() => Log.Append("T");
    public void Check() => Log.Append("C");

    public void Run(int n)
    {
        Count = 0;
        do
        {
            Tick();
            Count++;
        } while (Count < n);
        Check();
    }
}