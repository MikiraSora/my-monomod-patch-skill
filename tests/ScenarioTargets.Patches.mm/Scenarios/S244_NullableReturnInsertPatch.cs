#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S244_NullableReturnInsert : S244_NullableReturnInsert
{
    // Inserted after First(), before TryGetName.
    public void MidNote() => Log.Append("[mid];");
}