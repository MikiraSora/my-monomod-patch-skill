#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S47_StaticGeneric : S47_StaticGeneric
{
    public extern static string orig_Identity<T>(T v);

    public static string Identity<T>(T v) => orig_Identity<T>(v) + "!";
}