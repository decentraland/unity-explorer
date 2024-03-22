using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.AvatarRendering.Emotes.Components
{
    public struct GetEmotesByPointersIntention : IAssetIntention, IEquatable<GetEmotesByPointersIntention>
    {
        public CancellationTokenSource CancellationTokenSource { get; }

        public IReadOnlyCollection<URN> Pointers { get; }
        public HashSet<URN> ProcessedPointers { get; }
        public HashSet<URN> SuccessfulPointers { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }
        public int Timeout { get; }

        public float elapsedTime;

        public GetEmotesByPointersIntention(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT) : this()
        {
            Pointers = pointers;
            CancellationTokenSource = new CancellationTokenSource();
            ProcessedPointers = new HashSet<URN>();
            SuccessfulPointers = new HashSet<URN>();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = timeout;
        }

        public bool Equals(GetEmotesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetEmotesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            Pointers != null ? Pointers.GetHashCode() : 0;
    }
}
