using System.Text;

namespace MonoModTestTargets;

public class S248_RecursiveInsert
{
    public StringBuilder Log { get; } = new();

    public int Factorial(int n)
    {
        if (n <= 1)
        {
            Log.Append("base;");
            return 1;
        }
        Log.Append($"f{n};");
        return n * Factorial(n - 1);
    }
}