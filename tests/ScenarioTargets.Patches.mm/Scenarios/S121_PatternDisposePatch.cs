#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S121_TryFinallyNoUsing : S121_TryFinallyNoUsing
{
    public extern string orig_Run();

    public string Run() => orig_Run() + "!";
}