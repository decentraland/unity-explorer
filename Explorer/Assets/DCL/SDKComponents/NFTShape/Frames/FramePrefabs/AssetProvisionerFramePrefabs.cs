using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.SDKComponents.NFTShape.Frames.FramePrefabs
{
    public class AssetProvisionerFramePrefabs : IFramePrefabs
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private IReadOnlyDictionary<NftFrameType, AbstractFrame>? prefabs;
        private AbstractFrame? defaultPrefab;

        public AssetProvisionerFramePrefabs(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public AbstractFrame FrameOrDefault(NftFrameType frameType)
        {
            if (prefabs == null || defaultPrefab == null)
                throw new Exception("First initialize prefabs");

            return prefabs.TryGetValue(frameType, out var prefab)
                ? prefab!
                : defaultPrefab;
        }

        public UniTask Initialize(
            IReadOnlyDictionary<NftFrameType, NFTShapeSettings.FrameRef> refs,
            NFTShapeSettings.FrameRef defaultRef,
            CancellationToken ct
        ) =>
            UniTask.WhenAll(
                DownloadPrefabs(refs, ct),
                DownloadDefaultPrefab(defaultRef, ct)
            );

        private async UniTask DownloadPrefabs(IReadOnlyDictionary<NftFrameType, NFTShapeSettings.FrameRef> refs, CancellationToken ct)
        {
            var map = new Dictionary<NftFrameType, AbstractFrame>(refs.Count);

            await refs.Select(async pair =>
            {
                var key = pair.Key;
                var reference = pair.Value;

                var result = await assetsProvisioner
                   .ProvideMainAssetAsync(reference.EnsureNotNull(), ct);

                map[key] = result.Value;
            })!;

            prefabs = map;
        }

        private async UniTask DownloadDefaultPrefab(NFTShapeSettings.FrameRef defaultRef, CancellationToken ct)
        {
            var result = await assetsProvisioner
               .ProvideMainAssetAsync(defaultRef.EnsureNotNull(), ct);

            defaultPrefab = result.Value;
        }
    }
}
