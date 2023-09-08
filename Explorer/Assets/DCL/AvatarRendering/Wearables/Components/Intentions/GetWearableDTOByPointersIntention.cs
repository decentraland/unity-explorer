using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

public struct GetWearableDTOByPointersIntention : IEquatable<GetWearableDTOByPointersIntention>, ILoadingIntention
{
    public CancellationTokenSource CancellationTokenSource { get; }
    public CommonLoadingArguments CommonArguments { get; set; }

    //TODO: Pool array
    public string[] Pointers;

    public bool Equals(GetWearableDTOByPointersIntention other) =>
        Equals(Pointers, other.Pointers) && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableDTOByPointersIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Pointers, CancellationTokenSource, CommonArguments);
}
