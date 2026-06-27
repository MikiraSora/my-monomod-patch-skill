using System.Text;

namespace MonoModTestTargets;

public class S222_SwitchBranchInsert
{
    public StringBuilder Log { get; } = new();

    public void CaseA() => Log.Append("A");
    public void CaseB() => Log.Append("B");
    public void CaseC() => Log.Append("C");
    public void Default() => Log.Append("D");

    public string Classify(int code)
    {
        switch (code)
        {
            case 1:
                CaseA();
                return "one";
            case 2:
                CaseB();
                return "two";
            default:
                Default();
                return "other";
        }
    }
}