namespace MonoModTestTargets;

public class S67_ParamsObjectArray
{
    public string Join(params object[] parts) => string.Join(",", parts);
}