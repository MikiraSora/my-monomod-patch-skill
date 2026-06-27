#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S120_DelegateField : S120_DelegateField
{
    public extern int orig_Apply(int n);

    public int Apply(int n) => orig_Apply(n) + 10;
}