using System.Text;

namespace MonoModTestTargets;

public class S202_MiddleInsertMarker
{
    public StringBuilder Log { get; } = new();

    public void Begin() => Log.Append("B");
    public void End() => Log.Append("E");

    public void Step()
    {
        Begin();
        End();
    }
}