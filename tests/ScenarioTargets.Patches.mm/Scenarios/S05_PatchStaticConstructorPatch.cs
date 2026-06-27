#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S05_PatchStaticConstructor : S05_PatchStaticConstructor
{
    public extern static void orig_cctor();

    [MonoModConstructor]
    public static void cctor()
    {
        orig_cctor();
        StaticMarker = "sctor:patched";
    }
}