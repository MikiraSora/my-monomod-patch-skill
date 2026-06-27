namespace MonoModTestTargets;

public class S84_GenericParamsMethod
{
    public string Compose<T>(string prefix, params T[] items) => prefix + ":" + string.Join(",", items);
}