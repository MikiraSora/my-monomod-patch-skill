using System;
using System.Text;

namespace MonoModTestTargets;

public class S203_MiddleInsertInTry
{
    public StringBuilder Log { get; } = new();

    public void A() => Log.Append("A");
    public void C() => Log.Append("C");

    public void SafeRun()
    {
        try
        {
            A();
            C();
        }
        catch (Exception)
        {
            Log.Append("!");
        }
    }
}