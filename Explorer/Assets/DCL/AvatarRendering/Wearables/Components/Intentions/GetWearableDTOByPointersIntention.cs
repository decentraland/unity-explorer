using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearableDTOByPointersIntention : IEquatable<GetWearableDTOByPointersIntention>, ILoadingIntention
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly List<URN> Pointers;

        public GetWearableDTOByPointersIntention(List<URN> pointers, CommonLoadingArguments commonArguments)
        {
            Pointers = pointers;
            CommonArguments = commonArguments;
        }

        public bool Equals(GetWearableDTOByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetWearableDTOByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);
    }
}
