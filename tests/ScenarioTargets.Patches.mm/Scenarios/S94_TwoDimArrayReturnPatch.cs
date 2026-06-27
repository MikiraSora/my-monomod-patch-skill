#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S94_TwoDimArrayReturn : S94_TwoDimArrayReturn
{
    public extern int[,] orig_Grid();

    public int[,] Grid()
    {
        var g = orig_Grid();
        // bump every cell by 1
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                g[i, j] += 1;
        return g;
    }
}