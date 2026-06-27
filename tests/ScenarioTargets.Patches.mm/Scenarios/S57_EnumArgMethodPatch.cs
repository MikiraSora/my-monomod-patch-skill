#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S57_EnumArg : S57_EnumArg
{
    public extern string orig_Name(S57_Color c);

    public string Name(S57_Color c) => orig_Name(c) + "!";
}