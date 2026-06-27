#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModPatch("global::MonoModTestTargets.S35_Point")]
internal class patch_S35_Point
{
    public int X;

    public extern int orig_Twice();

    public int Twice() => orig_Twice() + 1;
}