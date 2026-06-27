using System.Text;

namespace MonoModTestTargets;

public class S243_ParamsArrayInsert
{
    public StringBuilder Log { get; } = new();

    public string Build(params string[] parts) => string.Join("-", parts);

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        First();
        var r = Build("a", "b", "c");
        Log.Append($"r={r};");
        Last();
    }
}