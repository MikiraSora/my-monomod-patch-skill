namespace MonoModTestTargets;

public class S46_Recursive
{
    public int Fact(int n) => n <= 1 ? 1 : n * Fact(n - 1);
}