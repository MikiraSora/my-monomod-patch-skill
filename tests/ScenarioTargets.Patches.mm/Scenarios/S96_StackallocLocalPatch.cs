#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S96_StackallocLocal : S96_StackallocLocal
{
    public extern int orig_Total(int n);

    public int Total(int n) => orig_Total(n) + 1;
}