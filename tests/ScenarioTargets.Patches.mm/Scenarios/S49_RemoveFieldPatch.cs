using MonoMod;

namespace MonoModTestTargets;

internal class patch_S49_RemoveMember : S49_RemoveMember
{
    [MonoModRemove]
    public string Extra() => "removed";
}