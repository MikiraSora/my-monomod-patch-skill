namespace MonoModTestTargets;

public class S87_BaseVirtual
{
    public virtual string Name() => "base";
}

public class S87_Derived : S87_BaseVirtual
{
    public override string Name() => "derived:" + base.Name();
}