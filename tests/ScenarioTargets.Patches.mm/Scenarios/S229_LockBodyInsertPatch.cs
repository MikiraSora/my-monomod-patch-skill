#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S229_LockBodyInsert : S229_LockBodyInsert
{
    // Inserted inside the lock body, before Locked().
    public void PreLocked() => Log.Append("[pre];");
}