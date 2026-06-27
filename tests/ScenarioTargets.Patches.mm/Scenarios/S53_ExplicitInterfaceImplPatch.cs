#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S53_ExplicitInterface : S53_ExplicitInterface
{
    // The target has no public CompareTo; add one that returns the patched value,
    // demonstrating adding a public method alongside the explicit interface impl.
    public int CompareTo(object obj) => 42;
}