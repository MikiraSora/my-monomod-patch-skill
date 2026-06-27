#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S115_JaggedArrayReturn : S115_JaggedArrayReturn
{
    public extern int[][] orig_Build();

    public int[][] Build()
    {
        var a = orig_Build();
        // append a new sub-array
        var r = new int[a.Length + 1][];
        for (int i = 0; i < a.Length; i++) r[i] = a[i];
        r[a.Length] = new[] { 9 };
        return r;
    }
}