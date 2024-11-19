using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class AllNftShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private NftShapeProperties nftShapeProperties = new ();
        [SerializeField]
        private bool visible = true;
        [SerializeField]
        private NFTShapeSettings settings = null!;
        [Header("Grid")]
        [SerializeField] private int countInRow = 5;
        [SerializeField] private float distanceBetween = 1f;

        private IComponentPool<PartitionComponent>? partitionComponentPool;

        private void Start()
        {
            LaunchAsync().Forget();
        }

        private async UniTask LaunchAsync()
        {
            var world = World.Create();

            partitionComponentPool = new ComponentPool.WithDefaultCtor<PartitionComponent>(
                component => component.Clear());

            var framesPrefabs = new AssetProvisionerFramePrefabs(new AddressablesProvisioner());
            var framesPool = new FramesPool(framesPrefabs);

            await framesPrefabs.InitializeAsync(
                settings.FramePrefabs(),
                settings.DefaultFrame(),
                destroyCancellationToken
            );

            new SeveralDemoWorld(
                    AllFrameTypes()
                       .Select(e => nftShapeProperties.With(e))
                       .Select(e => new WarmUpSettingsNftShapeDemoWorld(world, framesPool,
                            framesPrefabs, e, () => visible, partitionComponentPool) as IDemoWorld)
                       .Append(new GridDemoWorld(world, countInRow, distanceBetween))
                       .ToList()
                ).SetUpAndRunAsync(destroyCancellationToken)
                 .Forget();
        }

        private void OnDestroy()
        {
            partitionComponentPool?.Dispose();
        }

        private IEnumerable<NftFrameType> AllFrameTypes() =>
            Enum.GetNames(typeof(NftFrameType)).Select(Enum.Parse<NftFrameType>);
    }
}
