namespace MonoModTestTargets;

public class S113_Base
{
    public string Tag;

    public S113_Base(string tag) => Tag = tag;
}

public class S113_Derived : S113_Base
{
    public S113_Derived(string tag) : base(tag) { }
}