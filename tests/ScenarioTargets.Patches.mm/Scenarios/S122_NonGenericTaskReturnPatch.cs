#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S122_NonGenericTaskReturn : S122_NonGenericTaskReturn
{
    public static bool Completed;

    public extern System.Threading.Tasks.Task orig_DoAsync();

    public async System.Threading.Tasks.Task DoAsync()
    {
        await orig_DoAsync();
        Completed = true;
    }
}