#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S117_CheckedContext : S117_CheckedContext
{
    public extern int orig_Mul(int a, int b);

    public int Mul(int a, int b) => orig_Mul(a, b) + 1;
}