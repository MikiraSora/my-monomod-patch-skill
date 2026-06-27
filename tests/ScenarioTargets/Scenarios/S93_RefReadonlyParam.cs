namespace MonoModTestTargets;

public class S93_RefReadonlyParam
{
    public int Sum(in int a, in int b) => a + b;
}