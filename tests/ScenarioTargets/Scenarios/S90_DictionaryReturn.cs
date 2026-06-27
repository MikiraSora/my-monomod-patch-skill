namespace MonoModTestTargets;

public class S90_DictionaryReturn
{
    public System.Collections.Generic.Dictionary<string, int> Build() => new() { ["a"] = 1 };
}