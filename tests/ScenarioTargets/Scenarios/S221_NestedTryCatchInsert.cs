using System;
using System.Text;

namespace MonoModTestTargets;

public class S221_NestedTryCatchInsert
{
    public StringBuilder Log { get; } = new();

    public void InnerThrow() => throw new FormatException("inner");

    public void Run()
    {
        try
        {
            try
            {
                InnerThrow();
            }
            catch (FormatException)
            {
                Log.Append("inner-caught");
            }
        }
        catch (Exception)
        {
            Log.Append("outer-caught");
        }
    }
}