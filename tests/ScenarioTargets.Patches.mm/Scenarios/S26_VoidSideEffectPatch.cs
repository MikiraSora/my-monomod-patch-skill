#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S26_VoidSideEffect : S26_VoidSideEffect
{
    public extern void orig_Tick();

    public void Tick()
    {
        orig_Tick();
        Count += 10;
    }
}