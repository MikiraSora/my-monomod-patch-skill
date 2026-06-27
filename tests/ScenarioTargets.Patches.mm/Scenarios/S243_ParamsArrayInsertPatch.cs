#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S243_ParamsArrayInsert : S243_ParamsArrayInsert
{
    // Inserted after First(), before Build call.
    public void MidNote() => Log.Append("[mid];");
}