namespace MonoModTestTargets;

public class S59_MultiTypeParam
{
    public string Pair<T, U>(T a, U b) => typeof(T).Name + "+" + typeof(U).Name + ":" + a + "," + b;
}