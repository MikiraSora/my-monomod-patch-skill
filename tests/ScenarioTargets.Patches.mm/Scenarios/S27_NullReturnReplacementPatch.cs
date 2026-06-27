using MonoMod;

namespace MonoModTestTargets;

internal class patch_S27_NullReturn : S27_NullReturn
{
    [MonoModReplace]
    public string Maybe() => "not-null";
}