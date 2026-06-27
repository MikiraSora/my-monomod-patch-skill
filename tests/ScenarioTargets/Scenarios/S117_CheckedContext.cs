namespace MonoModTestTargets;

public class S117_CheckedContext
{
    public int Mul(int a, int b)
    {
        checked { return a * b; }
    }
}