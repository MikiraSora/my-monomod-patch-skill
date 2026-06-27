#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S119_InitOnly : S119_InitOnly
{
    public extern string orig_Greet();

    public string Greet() => orig_Greet() + "!";
}