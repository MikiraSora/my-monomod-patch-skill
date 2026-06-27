using System.Text;

namespace MonoModTestTargets;

public class S215_StaticInsert
{
    public static StringBuilder SharedLog { get; } = new();

    public static void StepA() => SharedLog.Append("A");
    public static void StepC() => SharedLog.Append("C");

    public static void RunStatic()
    {
        StepA();
        StepC();
    }
}