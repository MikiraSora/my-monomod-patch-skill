#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S89_UsingDisposePattern : S89_UsingDisposePattern
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}