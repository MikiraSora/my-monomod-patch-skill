using System.Text;

namespace MonoModTestTargets;

public class S212_VirtualChainInsert
{
    public StringBuilder Log { get; } = new();

    public virtual string GetName() => "name";

    public string Build()
    {
        string n = GetName();
        Log.Append(n);
        return n;
    }
}