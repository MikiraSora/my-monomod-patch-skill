#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S230_UsingBodyInsert : S230_UsingBodyInsert
{
    // Inserted inside the using body, before Inner().
    public void PreInner() => Log.Append("[pre];");
}