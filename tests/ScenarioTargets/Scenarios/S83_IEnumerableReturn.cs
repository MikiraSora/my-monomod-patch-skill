namespace MonoModTestTargets;

public class S83_IEnumerableReturn
{
    public System.Collections.Generic.IEnumerable<int> Range() => new[] { 1, 2, 3 };
}