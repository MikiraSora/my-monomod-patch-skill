namespace MonoModTestTargets;

public readonly struct S54_ReadonlyStruct
{
    public readonly int Value;

    public S54_ReadonlyStruct(int v) => Value = v;

    public int Double() => Value * 2;
}