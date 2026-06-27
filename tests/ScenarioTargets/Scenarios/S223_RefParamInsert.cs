using System.Text;

namespace MonoModTestTargets;

public class S223_RefParamInsert
{
    public StringBuilder Log { get; } = new();
    public int Total { get; set; }

    public void Bump(ref int value, int delta)
    {
        value += delta;
        Log.Append($"bump:{value};");
    }

    public void Process()
    {
        int x = 0;
        Bump(ref x, 10);
        Bump(ref x, 20);
        Total = x;
    }
}