namespace MonoModTestTargets;

public class S15_GenericMethod
{
    public string Format<T>(T v) => "fmt:" + v;
}