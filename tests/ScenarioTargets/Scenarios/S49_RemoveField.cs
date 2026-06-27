namespace MonoModTestTargets;

public class S49_RemoveMember
{
    public string Keep() => "keep";

    // No method body anywhere references Extra; safe for [MonoModRemove].
    public string Extra() => "extra";
}