#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S80_ConstFieldAdd : S80_ConstFieldAdd
{
    // const fields: their constant values ARE copied (metadata, not ctor IL).
    public const string Extra = "EXTRA";

    public extern string orig_Label();

    public string Label() => orig_Label() + ":" + Extra;
}