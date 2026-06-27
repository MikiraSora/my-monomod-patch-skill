#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S68_TryFinallyWrap : S68_TryFinallyWrap
{
    public extern string orig_Render();

    public string Render()
    {
        try
        {
            return orig_Render() + "!";
        }
        finally
        {
            CleanedUp = true;
        }
    }
}