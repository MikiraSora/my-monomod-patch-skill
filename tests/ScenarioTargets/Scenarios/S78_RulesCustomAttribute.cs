namespace MonoModTestTargets;

public struct S78_Result
{
    public int Code;
}

public class S78_ValueTypeReturn
{
    public S78_Result Build() => new S78_Result { Code = 1 };
}