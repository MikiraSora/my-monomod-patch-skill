namespace MonoModTestTargets;

public struct S91_Handle
{
    public int Value;
}

public class S91_StructParamMethod
{
    public int Read(S91_Handle h) => h.Value;
}