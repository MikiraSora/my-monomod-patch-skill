#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S24_NoNew : S24_NoNew
{
    public extern string orig_Exists();

    public string Exists() => orig_Exists() + "!";

    [MonoModNoNew]
    public string NotInTarget() => "should-not-be-added";
}