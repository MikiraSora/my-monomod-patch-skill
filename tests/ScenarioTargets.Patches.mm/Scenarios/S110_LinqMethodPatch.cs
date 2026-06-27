#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S110_LinqMethod : S110_LinqMethod
{
    public extern int orig_SumEvens(int[] values);

    public int SumEvens(int[] values) => orig_SumEvens(values) + 1;
}