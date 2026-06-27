#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S28_Box<T> : S28_Box<T>
{
    public extern string orig_Show();

    public string Show() => orig_Show() + "!";
}