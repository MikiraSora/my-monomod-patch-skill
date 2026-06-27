#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S74_StaticReadonlyField : S74_StaticReadonlyField
{
    public extern string orig_Reveal();

    public string Reveal() => orig_Reveal() + "!";
}