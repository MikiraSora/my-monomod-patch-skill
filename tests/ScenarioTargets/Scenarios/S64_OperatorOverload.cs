namespace MonoModTestTargets;

public class S64_OperatorOverload
{
    public int Value;

    public S64_OperatorOverload(int v) => Value = v;

    public static S64_OperatorOverload operator +(S64_OperatorOverload a, S64_OperatorOverload b) =>
        new S64_OperatorOverload(a.Value + b.Value);
}