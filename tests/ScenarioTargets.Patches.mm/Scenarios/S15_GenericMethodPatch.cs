#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S15_GenericMethod : S15_GenericMethod
{
    public extern string orig_Format<T>(T v);

    public string Format<T>(T v) => "[" + orig_Format<T>(v) + "]";
}