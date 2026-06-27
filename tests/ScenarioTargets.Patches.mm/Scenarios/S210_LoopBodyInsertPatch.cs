#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S210_LoopBodyInsert : S210_LoopBodyInsert
{
    // Inserted inside the for-loop, before Log.Append(i) on each iteration.
    public void Tick() => Log.Append("T");
}