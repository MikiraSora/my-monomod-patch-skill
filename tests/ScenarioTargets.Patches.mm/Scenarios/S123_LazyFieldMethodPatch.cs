#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S123_LazyFieldMethod : S123_LazyFieldMethod
{
    public extern int orig_Get();

    public int Get() => orig_Get() + 1;
}