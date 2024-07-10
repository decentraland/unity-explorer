using System;
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
using DCL.Diagnostics;
using SceneRunner.Scene;
using UnityEngine;
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
        private readonly ISceneData sceneData;

        internal LoadGltfContainerSystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer, ISceneData sceneData) : base(world)
        {
            this.eventsBuffer = eventsBuffer;
            this.sceneData = sceneData;
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
            GltfContainerComponent component;
            if (!sceneData.TryGetHash(sdkComponent.Src, out string hash))
            {
                ReportHub.LogWarning($"Hash does not exist for asset with name {sdkComponent.Src} in the scene content", GetReportCategory());
                component = GltfContainerComponent.CreateFaulty(sdkComponent.Src);
            }
            else
            {
                // It's not the best idea to pass Transform directly but we rely on cancellation source to cancel if the entity dies
                var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, hash ,new CancellationTokenSource()), partitionComponent);
                component = new GltfContainerComponent(sdkComponent.GetVisibleMeshesCollisionMask(), sdkComponent.GetInvisibleMeshesCollisionMask(), promise);
                component.State = LoadingState.Loading;
            }
            
            eventsBuffer.Add(entity, component);
            World.Add(entity, component);
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
                    if (!sceneData.TryGetHash(sdkComponent.Src, out string hash))
                    {
                        ReportHub.LogWarning($"Hash does not exist for asset with name {sdkComponent.Src} in the scene content", GetReportCategory());
                        component.SetFaulty(sdkComponent.Src);
                    }
                    else
                    {
                        var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src,hash, new CancellationTokenSource()), partitionComponent);
                        component.Promise = promise;
                        component.State = LoadingState.Loading;
                    }
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
