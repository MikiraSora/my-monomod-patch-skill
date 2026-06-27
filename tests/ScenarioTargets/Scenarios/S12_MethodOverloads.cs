namespace MonoModTestTargets;

public class S12_MethodOverloads
{
    public string Do(int x) => "int:" + x;

    public string Do(string s) => "str:" + s;
}