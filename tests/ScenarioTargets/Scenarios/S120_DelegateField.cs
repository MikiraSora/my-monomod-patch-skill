namespace MonoModTestTargets;

public class S120_DelegateField
{
    public System.Func<int, int> Transform = x => x + 1;

    public int Apply(int n) => Transform(n);
}