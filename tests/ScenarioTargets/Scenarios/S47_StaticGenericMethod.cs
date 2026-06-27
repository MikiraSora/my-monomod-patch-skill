namespace MonoModTestTargets;

public class S47_StaticGeneric
{
    public static string Identity<T>(T v) => "id:" + v;
}