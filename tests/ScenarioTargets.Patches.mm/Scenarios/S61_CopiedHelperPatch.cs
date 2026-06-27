#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S61_CopiedHelper : S61_CopiedHelper
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+" + Suffix();

    // Not [MonoModIgnore]: this method is copied into the target type and
    // can be called from the patched method body.
    public string Suffix() => "copied";
}