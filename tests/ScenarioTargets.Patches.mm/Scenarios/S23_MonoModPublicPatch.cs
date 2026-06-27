#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S23_MonoModPublic : S23_MonoModPublic
{
    public extern string orig_Hidden();

    [MonoModPublic]
    public string Hidden() => orig_Hidden() + "!";
}