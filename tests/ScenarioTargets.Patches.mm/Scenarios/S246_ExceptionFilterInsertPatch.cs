#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S246_ExceptionFilterInsert : S246_ExceptionFilterInsert
{
    // Inserted in catch-with-filter block, before HandleError().
    public void PreHandle() => Log.Append("[pre];");
}