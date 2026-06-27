#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S55_NullableReturn : S55_NullableReturn
{
    public extern int? orig_Find(int key);

    public int? Find(int key)
    {
        var r = orig_Find(key);
        return r.HasValue ? r.Value + 100 : (int?)null;
    }
}