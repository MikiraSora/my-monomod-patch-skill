#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S04_PatchInstanceConstructor : S04_PatchInstanceConstructor
{
    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        Marker = "ctor:patched";
    }
}