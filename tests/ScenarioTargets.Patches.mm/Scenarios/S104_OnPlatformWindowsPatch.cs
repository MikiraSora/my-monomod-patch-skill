#pragma warning disable CS0626
using MonoMod;
using MonoMod.Utils;

namespace MonoModTestTargets;

[MonoModOnPlatform(OSKind.Windows)]
internal class patch_S104_OnPlatformWindows : S104_OnPlatformWindows
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}