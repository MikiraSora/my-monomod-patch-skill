namespace MonoModTestTargets;

public class S108_Singleton
{
    public static S108_Singleton Instance { get; } = new();

    public string Tag() => "singleton";
}