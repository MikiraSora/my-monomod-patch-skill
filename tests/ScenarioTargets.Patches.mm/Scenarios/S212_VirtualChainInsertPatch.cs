#pragma warning disable CS0626

using System.Text;

namespace MonoModTestTargets;

internal class patch_S212_VirtualChainInsert : S212_VirtualChainInsert
{
    // Inserted after GetName() (callvirt) and before Log.Append(n).
    public void PostProcess() => Log.Append("[post]");
}