#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S97_ForceCallNonVirtual : S97_ForceCallNonVirtual
{
    public extern int orig_Compute();

    // [MonoModForceCallvirt] forces calls to Compute to use callvirt (null-check).
    // The patched method itself calls orig_Compute; behavior must still be correct.
    [MonoModForceCallvirt]
    public int Compute() => orig_Compute() + 5;
}