using System.Text;

namespace MonoModTestTargets;

public class S242_GotoFlowInsert
{
    public StringBuilder Log { get; } = new();

    public void Enter() => Log.Append("enter;");
    public void Exit() => Log.Append("exit;");

    public void Run(int n)
    {
        int i = 0;
        Enter();
    loop:
        if (i >= n) goto done;
        Log.Append(i);
        i++;
        goto loop;
    done:
        Exit();
    }
}