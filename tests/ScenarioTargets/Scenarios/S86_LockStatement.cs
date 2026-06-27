namespace MonoModTestTargets;

public class S86_LockStatement
{
    private readonly object _gate = new();

    public int Run()
    {
        lock (_gate)
        {
            return 42;
        }
    }
}