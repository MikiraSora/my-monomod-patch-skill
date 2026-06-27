#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S105_ForeachMethod : S105_ForeachMethod
{
    public extern int orig_Sum(int[] values);

    public int Sum(int[] values) => orig_Sum(values) + 1;
}