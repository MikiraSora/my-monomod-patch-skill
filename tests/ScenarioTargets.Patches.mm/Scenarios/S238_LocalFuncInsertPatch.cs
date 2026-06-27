#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S238_LocalFuncInsert : S238_LocalFuncInsert
{
    // Inserted after Before(), before LocalSquare call.
    public void MidNote() => Log.Append("[mid];");
}