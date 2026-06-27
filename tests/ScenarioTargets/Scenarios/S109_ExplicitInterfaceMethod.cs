namespace MonoModTestTargets;

public interface S109_IFoo
{
    string Bar();
}

public class S109_ExplicitInterfaceMethod : S109_IFoo
{
    string S109_IFoo.Bar() => "bar";
}