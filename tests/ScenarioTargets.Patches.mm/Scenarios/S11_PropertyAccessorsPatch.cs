#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S11_PropertyAccessors : S11_PropertyAccessors
{
    public extern string orig_get_Value();
    public extern void orig_set_Value(string value);

    public string Value
    {
        get => orig_get_Value() + ":get";
        set => orig_set_Value(value + ":set");
    }
}