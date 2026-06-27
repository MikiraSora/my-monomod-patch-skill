#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S45_Circle : S45_Circle
{
    public extern string orig_Draw();

    public string Draw() => "[" + orig_Draw() + "]";
}