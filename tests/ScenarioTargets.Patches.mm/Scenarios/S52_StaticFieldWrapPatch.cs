#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S52_StaticFieldWrap : S52_StaticFieldWrap
{
    public extern static int orig_ReadCounter();

    public static int ReadCounter() => orig_ReadCounter() + 1000;
}