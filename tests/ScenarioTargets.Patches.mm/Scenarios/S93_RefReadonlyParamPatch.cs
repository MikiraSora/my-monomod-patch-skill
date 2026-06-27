#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S93_RefReadonlyParam : S93_RefReadonlyParam
{
    public extern int orig_Sum(in int a, in int b);

    public int Sum(in int a, in int b) => orig_Sum(in a, in b) + 100;
}