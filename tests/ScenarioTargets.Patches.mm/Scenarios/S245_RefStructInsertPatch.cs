#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S245_RefStructInsert : S245_RefStructInsert
{
    // Inserted after First(), before SumSpan call.
    public void MidNote() => Log.Append("[mid];");
}