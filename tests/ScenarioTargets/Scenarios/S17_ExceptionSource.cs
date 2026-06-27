namespace MonoModTestTargets;

public class S17_ExceptionSource
{
    public string Risky() => throw new InvalidOperationException("boom");
}