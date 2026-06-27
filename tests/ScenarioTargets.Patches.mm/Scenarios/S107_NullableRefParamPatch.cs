#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S107_NullableRefParam : S107_NullableRefParam
{
    public extern string orig_Greet(string? name);

    public string Greet(string? name) => orig_Greet(name) + "!";
}