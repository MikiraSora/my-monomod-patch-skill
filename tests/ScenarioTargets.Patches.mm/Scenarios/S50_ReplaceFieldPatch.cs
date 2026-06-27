#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S50_ArrayReturn : S50_ArrayReturn
{
    public extern int[] orig_Pair();

    public int[] Pair()
    {
        var a = orig_Pair();
        var r = new int[a.Length + 1];
        for (int i = 0; i < a.Length; i++) r[i] = a[i];
        r[a.Length] = 3;
        return r;
    }
}