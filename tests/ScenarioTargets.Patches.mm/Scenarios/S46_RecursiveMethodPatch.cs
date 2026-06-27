#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S46_Recursive : S46_Recursive
{
    public extern int orig_Fact(int n);

    public int Fact(int n)
    {
        // Wrap recursion: orig_ calls the (patched) Fact, so each level adds suffix +1
        return orig_Fact(n) + 1;
    }
}