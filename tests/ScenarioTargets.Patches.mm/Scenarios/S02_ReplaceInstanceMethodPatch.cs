namespace MonoModTestTargets;

internal class patch_S02_ReplaceInstanceMethod : S02_ReplaceInstanceMethod
{
    public int Score(int x) => x + 100;
}