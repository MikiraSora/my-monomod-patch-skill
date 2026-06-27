namespace MonoModTestTargets;

public class S122_NonGenericTaskReturn
{
    public async System.Threading.Tasks.Task DoAsync()
    {
        await System.Threading.Tasks.Task.Yield();
    }
}