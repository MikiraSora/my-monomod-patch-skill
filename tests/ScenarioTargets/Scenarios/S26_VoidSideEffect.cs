namespace MonoModTestTargets;

public class S26_VoidSideEffect
{
    public static int Count;

    public void Tick()
    {
        Count += 1;
    }
}