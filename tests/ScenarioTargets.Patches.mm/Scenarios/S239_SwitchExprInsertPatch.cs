#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S239_SwitchExprInsert : S239_SwitchExprInsert
{
    // Inserted after Start(), before Classify call.
    public void MidNote() => Log.Append("[mid];");
}