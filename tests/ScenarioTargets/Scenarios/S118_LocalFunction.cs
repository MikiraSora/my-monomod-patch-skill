namespace MonoModTestTargets;

public class S118_LocalFunction
{
    public int Compute(int n)
    {
        int Doubler(int x) => x * 2;
        return Doubler(n);
    }
}