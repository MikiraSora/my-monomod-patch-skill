#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S84_GenericParamsMethod : S84_GenericParamsMethod
{
    public extern string orig_Compose<T>(string prefix, T[] items);

    public string Compose<T>(string prefix, T[] items) => "[" + orig_Compose<T>(prefix, items) + "]";
}