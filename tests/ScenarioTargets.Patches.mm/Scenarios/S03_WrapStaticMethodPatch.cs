#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S03_WrapStaticMethod : S03_WrapStaticMethod
{
    public extern static string orig_Echo(string s);

    public static string Echo(string s) => orig_Echo(s) + "!";
}