#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModTargetModule("MonoModTestTargets")]
internal class patch_S102_TargetModuleMatch : S102_TargetModuleMatch
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}