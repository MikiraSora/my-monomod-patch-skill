#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S234_DoWhileInsert : S234_DoWhileInsert
{
    // Inserted inside do-while body, before Tick().
    public void PreTick() => Log.Append("[pre];");
}