#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S118_LocalFunction : S118_LocalFunction
{
    public extern int orig_Compute(int n);

    public int Compute(int n) => orig_Compute(n) + 1;
}