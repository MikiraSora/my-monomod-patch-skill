#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S36_IgnoredHelperType : S36_IgnoredHelperType
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "+p";
}

[MonoModIgnore]
internal static class S36_Helpers
{
    public static string Tag() => "helper";
}