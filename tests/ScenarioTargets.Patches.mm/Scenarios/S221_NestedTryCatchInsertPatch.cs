#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S221_NestedTryCatchInsert : S221_NestedTryCatchInsert
{
    // Inserted inside the inner catch block, before Log.Append("inner-caught").
    public void HandleInnerCatch() => Log.Append("[handled]");
}