#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModIfFlag("s98_on", true)]
internal class patch_S98_IfFlagInclude : S98_IfFlagInclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}