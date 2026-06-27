using MonoMod;

namespace MonoModTestTargets;

internal class patch_S22_RemoveMember : S22_RemoveMember
{
    // Remove Extra() entirely from the patched type; Keep() stays untouched.
    [MonoModRemove]
    public string Extra() => "removed";
}