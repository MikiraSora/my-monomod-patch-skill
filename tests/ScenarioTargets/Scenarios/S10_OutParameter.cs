namespace MonoModTestTargets;

public class S10_OutParameter
{
    public bool TryGet(out int r)
    {
        r = 1;
        return true;
    }
}