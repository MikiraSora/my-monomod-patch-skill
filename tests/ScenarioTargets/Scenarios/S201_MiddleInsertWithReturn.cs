namespace MonoModTestTargets;

public class S201_MiddleInsertWithReturn
{
    public int Value { get; set; }
    public int Recorded { get; set; }

    public int Compute()
    {
        Value = 42;
        return Value;
    }

    public void Done() => Value += 100;

    public void Process()
    {
        int r = Compute();
        Done();
    }
}