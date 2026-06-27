#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S247_NestedTryInTryInsert : S247_NestedTryInTryInsert
{
    // Inserted after StepA(), before inner try block.
    public void MidNote() => Log.Append("[mid];");
}