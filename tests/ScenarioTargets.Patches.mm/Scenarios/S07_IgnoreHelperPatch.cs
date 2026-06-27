#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S07_IgnoreHelper : S07_IgnoreHelper
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+patched";

    [MonoModIgnore]
    private static string Helper() => "ignored";
}