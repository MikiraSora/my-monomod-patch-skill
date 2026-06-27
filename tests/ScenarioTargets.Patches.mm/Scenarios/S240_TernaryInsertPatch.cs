#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S240_TernaryInsert : S240_TernaryInsert
{
    // Inserted after the ternary expression result is stored, before Log.Append.
    public void MidNote() => Log.Append("[mid];");
}