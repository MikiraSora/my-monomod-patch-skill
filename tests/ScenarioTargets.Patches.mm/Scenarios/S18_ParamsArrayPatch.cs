#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S18_ParamsArray : S18_ParamsArray
{
    public extern string orig_Join(string[] parts);

    public string Join(string[] parts) => orig_Join(parts) + "!";
}