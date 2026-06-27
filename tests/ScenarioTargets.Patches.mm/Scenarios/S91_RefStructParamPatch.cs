#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S91_StructParamMethod : S91_StructParamMethod
{
    public extern int orig_Read(S91_Handle h);

    public int Read(S91_Handle h) => orig_Read(h) + 10;
}