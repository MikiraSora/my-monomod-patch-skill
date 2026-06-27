using System;
using System.Text;

namespace MonoModTestTargets;

public class S214_CatchBlockInsert
{
    public StringBuilder Log { get; } = new();

    public void ThrowIt()
    {
        throw new InvalidOperationException("boom");
    }

    public void SafeExec()
    {
        try
        {
            ThrowIt();
        }
        catch (InvalidOperationException)
        {
            Log.Append("caught");
        }
    }
}