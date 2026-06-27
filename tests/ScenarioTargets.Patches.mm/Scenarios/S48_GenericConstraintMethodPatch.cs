#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S48_Constraint : S48_Constraint
{
    public extern string orig_Show<T>(T v) where T : System.IEquatable<T>;

    public string Show<T>(T v) where T : System.IEquatable<T> => orig_Show<T>(v) + "!";
}