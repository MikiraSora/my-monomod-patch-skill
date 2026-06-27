namespace MonoModTestTargets;

public class S48_Constraint
{
    public string Show<T>(T v) where T : System.IEquatable<T> => "c:" + v;
}