using DCL.Ipfs;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Intention for loading a scene's <see cref="ISSDescriptor"/>. Equality is by scene id so the
    ///     streamable cache dedupes concurrent requests across the LOD path and the SDK runtime path.
    ///     The v49 manifest gate lives inside <see cref="LoadISSDescriptorSystem"/> — pre-v49 scenes get
    ///     a cached <see cref="ISSDescriptor.NONE"/> result without any HTTP work.
    /// </summary>
    public struct GetISSDescriptor : ILoadingIntention, IEquatable<GetISSDescriptor>
    {
        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly string SceneId;
        public readonly AssetBundleManifestVersion? ManifestVersion;

        public GetISSDescriptor(string sceneId, AssetBundleManifestVersion? manifestVersion)
        {
            SceneId = sceneId;
            ManifestVersion = manifestVersion;
            // URL is just an identifier here — the loader builds the real URL from sceneId + the configured streaming-assets domain.
            CommonArguments = new CommonLoadingArguments(sceneId);
        }

        public static GetISSDescriptor For(SceneEntityDefinition definition) =>
            new (definition.id, definition.assetBundleManifestVersion);

        public bool Equals(GetISSDescriptor other) =>
            SceneId == other.SceneId;

        public override bool Equals(object obj) =>
            obj is GetISSDescriptor other && Equals(other);

        public override int GetHashCode() =>
            SceneId?.GetHashCode() ?? 0;

        public override string ToString() =>
            $"Get ISS Descriptor: {SceneId}";
    }
}
