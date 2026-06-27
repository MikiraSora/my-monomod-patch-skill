#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S17_ExceptionSource : S17_ExceptionSource
{
    public extern string orig_Risky();

    public string Risky()
    {
        try
        {
            return orig_Risky();
        }
        catch (InvalidOperationException)
        {
            return "safe";
        }
    }
}