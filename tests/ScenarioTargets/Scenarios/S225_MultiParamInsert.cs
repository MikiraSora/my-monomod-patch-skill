using System.Text;

namespace MonoModTestTargets;

public class S225_MultiParamInsert
{
    public StringBuilder Log { get; } = new();
    public int Width { get; set; } = 100;
    public int Height { get; set; } = 200;

    public void Start() { }
    public void End() { }

    public void Run()
    {
        Start();
        End();
    }
}