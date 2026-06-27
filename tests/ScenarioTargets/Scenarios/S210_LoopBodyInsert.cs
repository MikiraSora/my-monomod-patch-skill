using System.Text;

namespace MonoModTestTargets;

public class S210_LoopBodyInsert
{
    public StringBuilder Log { get; } = new();

    public void Start() => Log.Append("S");
    public void Finish() => Log.Append("F");

    public void Run(int n)
    {
        Start();
        for (int i = 0; i < n; i++)
        {
            Log.Append(i);
        }
        Finish();
    }
}