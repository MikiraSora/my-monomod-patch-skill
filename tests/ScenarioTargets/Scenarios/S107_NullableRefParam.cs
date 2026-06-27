namespace MonoModTestTargets;

public class S107_NullableRefParam
{
    public string Greet(string? name) => "hi " + (name ?? "anon");
}