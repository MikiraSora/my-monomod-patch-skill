#pragma warning disable CS0626

namespace MonoModTestTargets;

internal class patch_S64_OperatorOverload : S64_OperatorOverload
{
    public patch_S64_OperatorOverload(int v) : base(v) { }

    // Declare op_Addition as a plain static method (not C# operator) so the patch
    // type is a valid containing type. MonoMod relinks the method by name/signature.
    public extern static S64_OperatorOverload orig_op_Addition(S64_OperatorOverload a, S64_OperatorOverload b);

    public static S64_OperatorOverload op_Addition(S64_OperatorOverload a, S64_OperatorOverload b)
    {
        var r = orig_op_Addition(a, b);
        r.Value += 1;
        return r;
    }
}