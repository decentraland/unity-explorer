using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetEmotesByPointersIntention : IAssetIntention, IDisposable, IEquatable<GetEmotesByPointersIntention>
    {
        private readonly List<URN> pointers;
        private bool isDisposed;

        public CancellationTokenSource CancellationTokenSource { get; }

        public IReadOnlyCollection<URN> Pointers => pointers;

        public HashSet<URN> SuccessfulPointers { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }

        public LoadTimeout Timeout;

        public GetEmotesByPointersIntention(List<URN> pointers,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT) : this()
        {
            this.pointers = pointers;
            CancellationTokenSource = new CancellationTokenSource();
            SuccessfulPointers = POINTERS_HASHSET_POOL.Get();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = new LoadTimeout(timeout, 0);
        }

        public bool Equals(GetEmotesByPointersIntention other) =>
            Equals(Pointers, other.Pointers);

        public override bool Equals(object obj) =>
            obj is GetEmotesByPointersIntention other && Equals(other);

        public override int GetHashCode() =>
            Pointers != null ? Pointers.GetHashCode() : 0;

        public void Dispose()
        {
            if (isDisposed) return;
            POINTERS_POOL.Release(pointers);
            POINTERS_HASHSET_POOL.Release(SuccessfulPointers);
            CancellationTokenSource.Cancel();
            isDisposed = true;
        }

        public bool IsTimeout(float dt)
        {
            // Timeout access returns a temporary value. We need to reassign the field or we lose the changes
            Timeout = new LoadTimeout(Timeout.Timeout, Timeout.ElapsedTime + dt);
            bool result = Timeout.IsTimeout;
            return result;
        }
    }
}
