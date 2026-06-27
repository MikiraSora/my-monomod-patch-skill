#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S92_StaticFieldCrossMethod : S92_StaticFieldCrossMethod
{
    public static int Cache;

    public extern static int orig_Read();

    public static int Read() => orig_Read() + Cache;

    public extern static void orig_cctor();

    [MonoModConstructor]
    public static void cctor()
    {
        orig_cctor();
        Cache = 7;
    }
}