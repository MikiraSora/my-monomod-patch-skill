#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S250_IndexAccessInsert : S250_IndexAccessInsert
{
    // Inserted after First(), before Data[1] access.
    public void MidNote() => Log.Append("[mid];");
}