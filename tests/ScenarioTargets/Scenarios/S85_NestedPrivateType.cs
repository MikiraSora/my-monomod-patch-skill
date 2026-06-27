namespace MonoModTestTargets;

public class S85_NestedPrivateOwner
{
    internal class Inner
    {
        public string Id() => "inner";
    }

    public string Access() => new Inner().Id();
}