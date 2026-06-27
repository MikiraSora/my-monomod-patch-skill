#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S65_FuncReturn : S65_FuncReturn
{
    public extern System.Func<int, int> orig_Getter();

    public System.Func<int, int> Getter()
    {
        var f = orig_Getter();
        return x => f(x) + 1;
    }
}