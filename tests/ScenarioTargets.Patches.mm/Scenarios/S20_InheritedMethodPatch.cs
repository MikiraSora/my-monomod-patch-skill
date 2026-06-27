#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S20_Base : S20_Base
{
    public extern string orig_Who();

    public string Who() => orig_Who() + "!";
}