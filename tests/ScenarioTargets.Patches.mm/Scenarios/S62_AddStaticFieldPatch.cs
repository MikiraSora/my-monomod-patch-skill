#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S62_AddStaticField : S62_AddStaticField
{
    public static string GlobalTag;

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
        if (GlobalTag is null)
            GlobalTag = "init";
    }
}