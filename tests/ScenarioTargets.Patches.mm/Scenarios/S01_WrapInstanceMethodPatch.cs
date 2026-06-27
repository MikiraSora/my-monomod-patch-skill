#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S01_WrapInstanceMethod : S01_WrapInstanceMethod
{
    public extern string orig_Greet(string name);

    public string Greet(string name) => "[P] " + orig_Greet(name).ToUpperInvariant();
}