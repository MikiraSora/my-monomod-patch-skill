#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S59_MultiTypeParam : S59_MultiTypeParam
{
    public extern string orig_Pair<T, U>(T a, U b);

    public string Pair<T, U>(T a, U b) => "[" + orig_Pair<T, U>(a, b) + "]";
}