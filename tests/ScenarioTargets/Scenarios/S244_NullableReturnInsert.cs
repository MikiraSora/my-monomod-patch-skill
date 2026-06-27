using System.Text;

namespace MonoModTestTargets;

public class S244_NullableReturnInsert
{
    public StringBuilder Log { get; } = new();

    public string? TryGetName() => "name";

    public void First() => Log.Append("1;");
    public void Last() => Log.Append("last;");

    public void Run()
    {
        First();
        var name = TryGetName();
        Log.Append($"name={name};");
        Last();
    }
}