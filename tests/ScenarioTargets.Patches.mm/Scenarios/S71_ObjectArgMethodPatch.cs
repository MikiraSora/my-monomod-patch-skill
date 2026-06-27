#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S71_ObjectArgMethod : S71_ObjectArgMethod
{
    public extern string orig_Describe(object o);

    public string Describe(object o) => orig_Describe(o) + "!";
}