using System.Text;

namespace MonoModTestTargets;

public class S213_StackConsumeInsert
{
    public StringBuilder Log { get; } = new();

    public string GetPrefix() => "pre-";
    public string GetSuffix() => "-suf";

    public string Build()
    {
        Log.Append(GetPrefix() + GetSuffix());
        return "result";
    }
}