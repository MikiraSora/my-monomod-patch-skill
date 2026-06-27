namespace MonoModTestTargets;

public class S09_RefParameter
{
    public void Bump(ref int x)
    {
        x += 1;
    }
}