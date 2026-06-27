#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S88_EarlyReturnNoOrig : S88_EarlyReturnNoOrig
{
    // No orig_ declared: full replacement. Negative code short-circuits to -1.
    public int Handle(int code) => code < 0 ? -1 : code * 2 + 1;
}