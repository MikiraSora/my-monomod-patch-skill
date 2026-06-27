namespace MonoModTestTargets;

public class S52_StaticFieldWrap
{
    public static int Counter = 0;

    public static int ReadCounter() => Counter;
}