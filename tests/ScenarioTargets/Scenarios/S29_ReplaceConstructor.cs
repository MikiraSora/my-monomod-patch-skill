namespace MonoModTestTargets;

public class S29_ReplaceConstructor
{
    public string Tag { get; set; } = "unset";

    public S29_ReplaceConstructor()
    {
        Tag = "orig";
    }
}