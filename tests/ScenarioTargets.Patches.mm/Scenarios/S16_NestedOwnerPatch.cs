#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S16_NestedOwner : S16_NestedOwner
{
    internal class patch_Inner : S16_NestedOwner.Inner
    {
        public extern string orig_Id();

        public string Id() => orig_Id() + "!";
    }
}