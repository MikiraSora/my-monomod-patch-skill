using System.Text;

namespace MonoModTestTargets;

public class S235_WhileInsert
{
    public StringBuilder Log { get; } = new();
    public int Count { get; set; }

    public void Step() => Log.Append("S");
    public void Done() => Log.Append("D");

    public void Run(int n)
    {
        Count = 0;
        while (Count < n)
        {
            Step();
            Count++;
        }
        Done();
    }
}