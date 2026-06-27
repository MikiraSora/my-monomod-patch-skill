#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S41_EventRaise : S41_EventRaise
{
    public extern void orig_Fire();

    public void Fire()
    {
        orig_Fire();
        Hits += 1;
    }
}