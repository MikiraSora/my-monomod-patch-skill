#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S242_GotoFlowInsert : S242_GotoFlowInsert
{
    // Inserted after Enter(), before the goto loop.
    public void MidNote() => Log.Append("[mid];");
}