namespace MonoModTestTargets;

public class S110_LinqMethod
{
    public int SumEvens(int[] values) => values.Where(v => v % 2 == 0).Sum();
}