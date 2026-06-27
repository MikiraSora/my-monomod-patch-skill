#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S108_Singleton : S108_Singleton
{
    public extern string orig_Tag();

    public string Tag() => orig_Tag() + "!";
}