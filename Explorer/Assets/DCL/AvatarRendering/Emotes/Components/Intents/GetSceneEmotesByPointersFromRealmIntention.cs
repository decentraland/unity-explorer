using AssetManagement;
using DCL.AvatarRendering.Wearables;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace DCL.AvatarRendering.Emotes
{
    public struct GetSceneEmoteFromRealmIntention : IEquatable<GetSceneEmoteFromRealmIntention>, IAssetIntention
    {
        public CancellationTokenSource CancellationTokenSource { get; }

        public string Hash { get; }
        public bool Loop { get; }
        public AssetSource PermittedSources { get; }
        public BodyShape BodyShape { get; }
        public int Timeout { get; }
        public bool IsModelProcessed;
        public bool IsAssetBundleProcessed;

        public float ElapsedTime;

        public GetSceneEmoteFromRealmIntention(string hash,
            bool loop,
            BodyShape bodyShape,
            AssetSource permittedSources = AssetSource.ALL,
            int timeout = StreamableLoadingDefaults.TIMEOUT) : this()
        {
            Hash = hash;
            Loop = loop;
            CancellationTokenSource = new CancellationTokenSource();
            PermittedSources = permittedSources;
            BodyShape = bodyShape;
            Timeout = timeout;
        }

        public bool Equals(GetSceneEmoteFromRealmIntention other) =>
            Hash == other.Hash && Loop == other.Loop && BodyShape.Equals(other.BodyShape);
    }
}
