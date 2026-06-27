#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

[MonoModIfFlag("s99_on", false)]
internal class patch_S99_IfFlagExclude : S99_IfFlagExclude
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}