using System;

namespace MonoModTestTargets;

public class S53_ExplicitInterface : IComparable
{
    int IComparable.CompareTo(object obj) => 0;
}