namespace MonoModTestTargets;

public class S116_SwitchExpression
{
    public string Classify(int n) => n switch
    {
        0 => "zero",
        1 => "one",
        _ => "many",
    };
}