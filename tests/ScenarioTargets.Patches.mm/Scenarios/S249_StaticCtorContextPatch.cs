#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S249_StaticCtorContext : S249_StaticCtorContext
{
    // Inserted between Log.Append("init;") and Tag="ready" in Init().
    public static void MidNote() => Log.Append("[mid];");
}