#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S67_ParamsObjectArray : S67_ParamsObjectArray
{
    public extern string orig_Join(object[] parts);

    public string Join(object[] parts) => orig_Join(parts) + "!";
}