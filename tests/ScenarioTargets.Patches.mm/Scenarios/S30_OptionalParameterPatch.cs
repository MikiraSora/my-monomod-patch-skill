#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S30_OptionalParameter : S30_OptionalParameter
{
    public extern string orig_Greet(string name, string punc);

    public string Greet(string name, string punc = ".") => orig_Greet(name, punc) + "!";
}