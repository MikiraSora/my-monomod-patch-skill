#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S43_AsyncMethod : S43_AsyncMethod
{
    public extern System.Threading.Tasks.Task<string> orig_FetchAsync();

    public async System.Threading.Tasks.Task<string> FetchAsync()
    {
        var r = await orig_FetchAsync();
        return r + "!";
    }
}