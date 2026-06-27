#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S226_TryFinallyInsert : S226_TryFinallyInsert
{
    // Inserted in the finally block, before Cleanup().
    public void MarkFinally() => Log.Append("[finally];");
}