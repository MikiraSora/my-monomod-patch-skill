namespace MonoModTestTargets;

public class S43_AsyncMethod
{
    public async System.Threading.Tasks.Task<string> FetchAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        return "fetched";
    }
}