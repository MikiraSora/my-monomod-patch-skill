namespace MonoModTestTargets;

public class S44_RefReturn
{
    private int _v = 1;

    public ref int Slot() => ref _v;
}