namespace MonoModTestTargets;

public class S65_FuncReturn
{
    public System.Func<int, int> Getter() => x => x * 2;
}