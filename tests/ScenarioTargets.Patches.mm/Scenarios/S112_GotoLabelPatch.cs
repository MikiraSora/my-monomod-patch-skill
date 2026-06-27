#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S112_GotoLabel : S112_GotoLabel
{
    public extern int orig_Loop(int n);

    public int Loop(int n) => orig_Loop(n) + 1;
}