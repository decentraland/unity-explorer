using CommunicationData.URLHelpers;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public readonly struct GetEmotesByPointersIntention : IAssetIntention, IEquatable<GetEmotesByPointersIntention>
    {
        public CancellationTokenSource CancellationTokenSource { get; }

        public IReadOnlyCollection<URN> Pointers { get; }
        public HashSet<URN> ProcessedPointers { get; }

        public GetEmotesByPointersIntention(IReadOnlyCollection<URN> pointers) : this()
        {
            Pointers = pointers;
            CancellationTokenSource = new CancellationTokenSource();
            ProcessedPointers = new HashSet<URN>();
        }

        public bool Equals(GetEmotesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetEmotesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            Pointers != null ? Pointers.GetHashCode() : 0;
    }
}
