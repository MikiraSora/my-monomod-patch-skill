#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S21_OriginalName : S21_OriginalName
{
    [MonoModOriginal]
    public extern string original_Code();

    [MonoModOriginalName("original_Code")]
    public string Code() => original_Code() + "!";
}