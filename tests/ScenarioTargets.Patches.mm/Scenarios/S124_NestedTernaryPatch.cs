#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S124_NestedTernary : S124_NestedTernary
{
    public extern string orig_Classify(int n);

    public string Classify(int n) => "[" + orig_Classify(n) + "]";
}