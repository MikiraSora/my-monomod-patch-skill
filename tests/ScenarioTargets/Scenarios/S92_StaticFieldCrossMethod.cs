namespace MonoModTestTargets;

public class S92_StaticFieldCrossMethod
{
    public static int Base = 0;

    static S92_StaticFieldCrossMethod()
    {
        Base = 0;
    }

    public static int Read() => Base;
}