namespace MonoModTestTargets;

public class S124_NestedTernary
{
    public string Classify(int n) => n == 0 ? "zero" : n < 0 ? "neg" : "pos";
}