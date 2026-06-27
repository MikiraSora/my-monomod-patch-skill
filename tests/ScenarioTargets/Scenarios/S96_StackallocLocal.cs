namespace MonoModTestTargets;

public class S96_StackallocLocal
{
    public int Total(int n)
    {
        Span<int> buf = stackalloc int[1];
        buf[0] = n;
        return buf[0];
    }
}