namespace MonoModTestTargets;

public class S25_Base
{
    public virtual string Virt() => "base-virt";
}

public class S25_Derived : S25_Base
{
    public override string Virt() => "derived";
}