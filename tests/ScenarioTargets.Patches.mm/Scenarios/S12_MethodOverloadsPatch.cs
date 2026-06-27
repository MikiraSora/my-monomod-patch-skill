#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S12_MethodOverloads : S12_MethodOverloads
{
    public extern string orig_Do(int x);

    public string Do(int x) => orig_Do(x) + "!";
}