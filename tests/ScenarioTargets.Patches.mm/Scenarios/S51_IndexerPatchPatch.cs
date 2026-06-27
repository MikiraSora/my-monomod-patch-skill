#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S51_Indexer : S51_Indexer
{
    public extern string orig_get_Item(int i);
    public extern void orig_set_Item(int i, string value);

    public string this[int i]
    {
        get => orig_get_Item(i) + "!";
        set => orig_set_Item(i, value + "#");
    }
}