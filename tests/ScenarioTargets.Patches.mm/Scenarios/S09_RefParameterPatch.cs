#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S09_RefParameter : S09_RefParameter
{
    public extern void orig_Bump(ref int x);

    public void Bump(ref int x)
    {
        orig_Bump(ref x);
        x += 10;
    }
}