namespace MonoModTestTargets;

public class S211_ParamInsert
{
    public int Counter { get; set; } = 10;
    public int LoggedValue { get; set; }

    public void Begin() { }
    public void End() => Counter += 5;

    public void Run()
    {
        Begin();
        End();
    }
}