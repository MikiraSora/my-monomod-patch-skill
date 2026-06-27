#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S215_StaticInsert : S215_StaticInsert
{
    // Static method inserted between static StepA() and StepC().
    public static void StepB() => SharedLog.Append("B");
}