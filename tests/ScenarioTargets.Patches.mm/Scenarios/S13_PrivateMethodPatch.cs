#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S13_PrivateMethod : S13_PrivateMethod
{
    public extern string orig_Secret();

    public string Secret() => orig_Secret() + "!";
}