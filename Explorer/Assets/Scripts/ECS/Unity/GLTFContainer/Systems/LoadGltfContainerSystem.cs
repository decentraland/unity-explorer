using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Components.Defaults;
using System.Threading;
using UnityEngine.Assertions;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.Unity.GLTFContainer.Asset.Components.GltfContainerAsset, ECS.Unity.GLTFContainer.Asset.Components.GetGltfContainerAssetIntention>;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Starts GltfContainerAsset loading initially or upon the SDK Component change
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [ThrottlingEnabled]
    public partial class LoadGltfContainerSystem : BaseUnityLoopSystem
    {
        private readonly EntityEventBuffer<GltfContainerComponent> eventsBuffer;

        internal LoadGltfContainerSystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer) : base(world)
        {
            this.eventsBuffer = eventsBuffer;
        }

        protected override void Update(float t)
        {
            StartLoadingQuery(World);
            ReconfigureGltfContainerQuery(World);
        }

        [Query]
        [None(typeof(GltfContainerComponent))]
        private void StartLoading(in Entity entity, ref PBGltfContainer sdkComponent, ref PartitionComponent partitionComponent)
        {
            // It's not the best idea to pass Transform directly but we rely on cancellation source to cancel if the entity dies
            var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, new CancellationTokenSource()), partitionComponent);
            var component = new GltfContainerComponent(sdkComponent.GetVisibleMeshesCollisionMask(), sdkComponent.GetInvisibleMeshesCollisionMask(), promise);
            component.State = LoadingState.Loading;
            World.Add(entity, component);
            eventsBuffer.Add(entity, component);
        }

        // SDK Component was changed
        [Query]
        private void ReconfigureGltfContainer(Entity entity, ref GltfContainerComponent component, ref PBGltfContainer sdkComponent, ref PartitionComponent partitionComponent)
        {
            if (!sdkComponent.IsDirty) return;

            switch (component.State)
            {
                // The source is changed, should start downloading over again
                case LoadingState.Unknown:
                    var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, new CancellationTokenSource()), partitionComponent);
                    component.Promise = promise;
                    component.State = LoadingState.Loading;
                    eventsBuffer.Add(entity, component);
                    return;

                // Clean-up is handled by ResetGltfContainerSystem so "InProgress" is not considered here
                // Do nothing if finished with error
                case LoadingState.Finished:
                    Assert.IsTrue(component.Promise.Result.HasValue);

                    // if promise was unsuccessful nothing to do
                    StreamableLoadingResult<GltfContainerAsset> result = component.Promise.Result.Value;

                    if (!result.Succeeded)
                        return;

                    ColliderLayer visibleCollisionMask = sdkComponent.GetVisibleMeshesCollisionMask();

                    if (visibleCollisionMask != component.VisibleMeshesCollisionMask)
                    {
                        component.VisibleMeshesCollisionMask = visibleCollisionMask;
                        ConfigureGltfContainerColliders.SetupVisibleColliders(ref component, result.Asset);
                    }

                    ColliderLayer invisibleCollisionMask = sdkComponent.GetInvisibleMeshesCollisionMask();

                    if (invisibleCollisionMask != component.InvisibleMeshesCollisionMask)
                    {
                        component.InvisibleMeshesCollisionMask = invisibleCollisionMask;
                        ConfigureGltfContainerColliders.SetupInvisibleColliders(ref component, result.Asset);
                    }

                    return;
            }
        }
    }
}
