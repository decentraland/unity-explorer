using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

public struct GetWearableByPointersIntention : IEquatable<GetWearableByPointersIntention>, ILoadingIntention
{
    public CancellationTokenSource CancellationTokenSource { get; }
    public CommonLoadingArguments CommonArguments { get; set; }

    public string BodyShape;

    //TODO: Pool array
    public string[] Pointers;

    //TODO: Pool array. Controlled and resolved in the consumer layer
    public Wearable[] results;


    public bool Equals(GetWearableByPointersIntention other) =>
        Equals(Pointers, other.Pointers) && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableByPointersIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Pointers, CancellationTokenSource, CommonArguments);

}
