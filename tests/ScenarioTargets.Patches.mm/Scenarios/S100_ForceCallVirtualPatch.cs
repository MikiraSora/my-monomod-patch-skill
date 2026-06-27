#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S100_ForceCallVirtual : S100_ForceCallVirtual
{
    public extern int orig_Compute();

    // [MonoModForceCall] forces calls to Compute to use call (non-virtual dispatch).
    [MonoModForceCall]
    public override int Compute() => orig_Compute() + 5;
}