namespace MonoModTestTargets;

public class S89_UsingDisposePattern
{
    public string Run()
    {
        using var scope = new S89_Scope();
        return "ran";
    }
}

public sealed class S89_Scope : System.IDisposable
{
    public bool Disposed;

    public void Dispose() => Disposed = true;
}