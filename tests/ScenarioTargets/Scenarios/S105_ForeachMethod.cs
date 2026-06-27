namespace MonoModTestTargets;

public class S105_ForeachMethod
{
    public int Sum(int[] values)
    {
        var total = 0;
        foreach (var v in values) total += v;
        return total;
    }
}