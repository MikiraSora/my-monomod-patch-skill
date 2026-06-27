#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S81_TupleReturn : S81_TupleReturn
{
    public extern (int a, string b) orig_Pair();

    public (int a, string b) Pair()
    {
        var t = orig_Pair();
        return (t.a + 10, t.b + "!");
    }
}