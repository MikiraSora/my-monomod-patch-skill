namespace MonoModTestTargets;

public class S123_LazyFieldMethod
{
    private readonly System.Lazy<int> _val = new(() => 42);

    public int Get() => _val.Value;
}