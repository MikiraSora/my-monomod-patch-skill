namespace MonoModTestTargets;

public class S111_ByRefPropertyField
{
    private int _v = 1;

    public ref int Value() => ref _v;
}