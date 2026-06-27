namespace MonoModTestTargets;

public class S31_InParameter
{
    public int Add(in int x) => x + 1;
}