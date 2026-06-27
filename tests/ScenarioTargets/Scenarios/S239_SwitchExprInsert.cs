using System.Text;

namespace MonoModTestTargets;

public class S239_SwitchExprInsert
{
    public StringBuilder Log { get; } = new();

    public void Start() => Log.Append("S;");
    public void End() => Log.Append("E;");

    public string Classify(int n) => n switch
    {
        0 => "zero",
        1 => "one",
        _ => "many"
    };

    public void Run()
    {
        Start();
        var label = Classify(2);
        Log.Append($"label={label};");
        End();
    }
}