namespace MonoModTestTargets;

public class S04_PatchInstanceConstructor
{
    public string Marker { get; set; } = "unset";

    public S04_PatchInstanceConstructor()
    {
        Marker = "ctor:orig";
    }
}