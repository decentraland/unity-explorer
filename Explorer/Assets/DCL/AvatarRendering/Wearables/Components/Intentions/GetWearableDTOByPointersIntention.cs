using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiling;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Wearables.Components.Intentions
{
    public struct GetWearableDTOByPointersIntention : IEquatable<GetWearableDTOByPointersIntention>, IPointersLoadingIntention
    {
        private readonly List<URN> pointers;
        private bool released;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly IReadOnlyList<URN> Pointers
        {
            get
            {
                if (released)
                    throw new Exception("Pointers have been released");

                return pointers;
            }
        }

        public GetWearableDTOByPointersIntention(List<URN> pointers, CommonLoadingArguments commonArguments)
        {
            this.pointers = pointers;
            CommonArguments = commonArguments;
            released = false;
            ProfilingCounters.GetWearablesIntentionAmount.Value++;
        }

        public bool Equals(GetWearableDTOByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetWearableDTOByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Pointers);

        public void ReleasePointers()
        {
            released = true;
            WearableComponentsUtils.POINTERS_POOL.Release(pointers);
            ProfilingCounters.GetWearablesIntentionAmount.Value--;
        }
    }
}
