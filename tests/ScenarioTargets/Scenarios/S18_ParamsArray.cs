namespace MonoModTestTargets;

public class S18_ParamsArray
{
    public string Join(params string[] parts) => string.Join(",", parts);
}