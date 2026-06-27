#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S85_NestedPrivateOwner : S85_NestedPrivateOwner
{
    internal class patch_Inner : S85_NestedPrivateOwner.Inner
    {
        public extern string orig_Id();

        public string Id() => orig_Id() + "!";
    }
}