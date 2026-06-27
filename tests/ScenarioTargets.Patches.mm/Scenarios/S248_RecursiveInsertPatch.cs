#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S248_RecursiveInsert : S248_RecursiveInsert
{
    // Inserted after Log.Append in the recursive branch, before the recursive call.
    public void PreRecurse() => Log.Append("[pre];");
}