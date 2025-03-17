using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using ECS.Prioritization.Components;
using System;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class NftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NFTShapeSettings settings = null!;

        private IComponentPool<PartitionComponent>? partitionComponentPool;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTask LaunchAsync()
        {
            partitionComponentPool = new ComponentPool.WithDefaultCtor<PartitionComponent>(
                component => component.Clear());

            var framesPrefabs = new AssetProvisionerFramePrefabs(new AddressablesProvisioner());

            var world = new WarmUpSettingsNftShapeDemoWorld(new FramesPool(framesPrefabs),
                framesPrefabs, nftShapeProperties, () => visible, partitionComponentPool);

            await framesPrefabs.InitializeAsync(
                settings.FramePrefabs(),
                settings.DefaultFrame(),
                destroyCancellationToken
            );

            await world.SetUpAndRunAsync(destroyCancellationToken);
        }

        private void OnDestroy()
        {
            partitionComponentPool?.Dispose();
        }
    }
}
