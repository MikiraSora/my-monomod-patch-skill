#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S73_RethrowWrap : S73_RethrowWrap
{
    public extern int orig_Parse(string s);

    public int Parse(string s)
    {
        try
        {
            return orig_Parse(s);
        }
        catch (System.FormatException ex)
        {
            throw new System.FormatException("wrapped:" + ex.Message);
        }
    }
}