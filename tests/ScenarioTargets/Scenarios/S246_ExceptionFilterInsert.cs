using System;
using System.Text;

namespace MonoModTestTargets;

public class S246_ExceptionFilterInsert
{
    public StringBuilder Log { get; } = new();

    public void ThrowIt()
    {
        throw new InvalidOperationException("filtered");
    }

    public void HandleError() => Log.Append("handled;");

    public void SafeExec()
    {
        try
        {
            ThrowIt();
        }
        catch (InvalidOperationException ex) when (ex.Message == "filtered")
        {
            HandleError();
        }
    }
}