#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S211_ParamInsert : S211_ParamInsert
{
    // Inserted between Begin() and End(), with Counter loaded as argument.
    public void LogValue(int value) => LoggedValue = value;
}