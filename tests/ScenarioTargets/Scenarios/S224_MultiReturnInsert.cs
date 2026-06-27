using System.Text;

namespace MonoModTestTargets;

public class S224_MultiReturnInsert
{
    public StringBuilder Log { get; } = new();

    public void MarkEarly() => Log.Append("early;");
    public void MarkLate() => Log.Append("late;");

    public string Evaluate(int n)
    {
        if (n < 0)
        {
            MarkEarly();
            return "neg";
        }

        MarkLate();
        return "non-neg";
    }
}