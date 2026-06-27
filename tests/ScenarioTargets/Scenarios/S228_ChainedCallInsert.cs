using System.Text;

namespace MonoModTestTargets;

public class S228_ChainedCallInsert
{
    public StringBuilder Log { get; } = new();

    public S228_ChainedCallInsert Self() { Log.Append("self;"); return this; }
    public void Done() => Log.Append("done;");

    public void Run()
    {
        Self().Done();
    }
}