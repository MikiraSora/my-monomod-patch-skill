#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S224_MultiReturnInsert : S224_MultiReturnInsert
{
    // Inserted after MarkEarly(), before ldstr "neg" in the early-return branch.
    public void AfterMarkEarly() => Log.Append("[after-early]");
}