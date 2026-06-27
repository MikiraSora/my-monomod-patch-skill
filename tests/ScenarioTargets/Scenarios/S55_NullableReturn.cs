namespace MonoModTestTargets;

public class S55_NullableReturn
{
    public int? Find(int key) => key > 0 ? key : null;
}