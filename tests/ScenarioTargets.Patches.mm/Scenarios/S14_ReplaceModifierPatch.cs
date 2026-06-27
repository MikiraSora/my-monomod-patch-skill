using MonoMod;

namespace MonoModTestTargets;

internal class patch_S14_ReplaceModifier : S14_ReplaceModifier
{
    [MonoModReplace]
    public string Mode() => "fast";
}