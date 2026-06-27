#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S31_InParameter : S31_InParameter
{
    public extern int orig_Add(in int x);

    public int Add(in int x) => orig_Add(in x) + 10;
}