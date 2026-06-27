namespace MonoModTestTargets;

public class S119_InitOnly
{
    public string Name { get; init; }

    public S119_InitOnly() => Name = "anon";

    public string Greet() => "hi " + Name;
}