using System;
using System.Text;

namespace MonoModTestTargets;

public class S247_NestedTryInTryInsert
{
    public StringBuilder Log { get; } = new();

    public void StepA() => Log.Append("A;");
    public void StepB() => Log.Append("B;");

    public void Run()
    {
        try
        {
            StepA();
            try
            {
                StepB();
            }
            catch (FormatException)
            {
                Log.Append("inner;");
            }
        }
        catch (Exception)
        {
            Log.Append("outer;");
        }
    }
}