#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S10_OutParameter : S10_OutParameter
{
    public extern bool orig_TryGet(out int r);

    public bool TryGet(out int r)
    {
        bool ok = orig_TryGet(out r);
        r += 100;
        return ok;
    }
}