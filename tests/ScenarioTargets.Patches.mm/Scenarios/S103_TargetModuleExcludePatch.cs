#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

// Target module name deliberately does NOT match the real target assembly,
// so MatchingConditionals returns false and the patch is skipped.
[MonoModTargetModule("SomeOtherAssembly")]
internal class patch_S103_TargetModuleExclude : S103_TargetModuleExclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}