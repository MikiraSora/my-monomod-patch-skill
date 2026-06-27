#pragma warning disable CS0626
using MonoMod;

namespace MonoModTestTargets;

internal class patch_S38_AddNewConstructor : S38_AddNewConstructor
{
    public string Note;

    public extern void orig_ctor();

    [MonoModConstructor]
    public void ctor()
    {
        orig_ctor();
    }

    [MonoModConstructor]
    public void ctor(string note)
    {
        orig_ctor();
        Note = note;
    }
}