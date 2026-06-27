#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S29_ReplaceConstructor : S29_ReplaceConstructor
{
    [MonoModReplace]
    [MonoModConstructor]
    public void ctor()
    {
        Tag = "replaced";
    }
}