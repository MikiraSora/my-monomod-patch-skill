namespace MonoModTestTargets;

public class S30_OptionalParameter
{
    public string Greet(string name, string punc = ".") => name + punc;
}