#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S82_PrivateStaticMethod : S82_PrivateStaticMethod
{
    private extern static string orig_Secret();

    private static string Secret() => orig_Secret() + "!";
}