#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S241_CheckedInsert : S241_CheckedInsert
{
    // Inserted after First(), before Log.Append in checked block.
    public void MidNote() => Log.Append("[mid];");
}