namespace MonoModTestTargets;

public class S51_Indexer
{
    private readonly string[] _data = new[] { "a", "b", "c" };

    public string this[int i]
    {
        get => _data[i];
        set => _data[i] = value;
    }
}