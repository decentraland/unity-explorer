using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Components.Defaults;
using System.Threading;
using DCL.Interaction.Utility;
using DCL.Utils;
using DCL.Utils.Time;
using ECS.StreamableLoading.AssetBundles;
using Global.AppArgs;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Networking;
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
        private readonly IEntityCollidersSceneCache entityCollidersSceneCache;

        private readonly Dictionary<string, GameObject> staticSceneGameObjects;

        internal LoadGltfContainerSystem(World world, EntityEventBuffer<GltfContainerComponent> eventsBuffer, ISceneData sceneData,
            IEntityCollidersSceneCache entityCollidersSceneCache) : base(world)
        {
            this.eventsBuffer = eventsBuffer;
            this.sceneData = sceneData;
            this.entityCollidersSceneCache = entityCollidersSceneCache;
            staticSceneGameObjects = sceneData.StaticSceneGameObjects;
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
            sdkComponent.IsDirty = false; // IsDirty is only relevant for ReConfiguration of the GLTFContainer

            bool tryGetHash = sceneData.TryGetHash(sdkComponent.Src, out string hash);

            if (tryGetHash && staticSceneGameObjects.ContainsKey(hash))
            {
                // It's not the best idea to pass Transform directly but we rely on cancellation source to cancel if the entity dies
                var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, hash, new CancellationTokenSource()), partitionComponent);
                var data = new AssetBundleData(null, null, staticSceneGameObjects[hash], new AssetBundleData[0]);
                data.description = $"AB:{hash}_BIG_ASSET_BUNDLE";
                World.Add(promise.Entity, new StreamableLoadingResult<AssetBundleData>(data));

                component = new GltfContainerComponent(
                    sdkComponent.GetVisibleMeshesCollisionMask(),
                    sdkComponent.GetInvisibleMeshesCollisionMask(),
                    promise);

                component.State = LoadingState.Loading;
                World.Add(entity, component);
                eventsBuffer.Add(entity, component);

                return;
            }

            if (!tryGetHash)
            {
                component = GltfContainerComponent.CreateFaulty(
                    GetReportData(),
                    new ArgumentException($"GLTF source {sdkComponent.Src} not found in the content")
                );

                World.Add(entity, component);
            }
            else
            {
                // It's not the best idea to pass Transform directly but we rely on cancellation source to cancel if the entity dies
                var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, hash, new CancellationTokenSource()), partitionComponent);
                component = new GltfContainerComponent(
                    sdkComponent.GetVisibleMeshesCollisionMask(),
                    sdkComponent.GetInvisibleMeshesCollisionMask(),
                    promise);
                component.State = LoadingState.Loading;
                World.Add(entity, component);
            }

            eventsBuffer.Add(entity, component);
        }

        // SDK Component was changed
        [Query]
        private void ReconfigureGltfContainer(Entity entity, ref GltfContainerComponent component, ref PBGltfContainer sdkComponent, ref PartitionComponent partitionComponent,
            ref CRDTEntity sdkEntity)
        {
            if (!sdkComponent.IsDirty) return;

            // Clean-up is handled by ResetGltfContainerSystem so "InProgress" is not considered here
            // Do nothing if finished with error

            // The source is changed, should start downloading over again (change check at
            // ResetGltfContainerSystem.InvalidatePromise() query)
            if (component.State == LoadingState.Unknown)
            {
                sdkComponent.IsDirty = false;

                if (!sceneData.TryGetHash(sdkComponent.Src, out string hash))
                    component.SetFaulty(
                        GetReportData(),
                        new ArgumentException($"GLTF source {sdkComponent.Src} not found in the content")
                    );
                else
                {
                    var promise = Promise.Create(World, new GetGltfContainerAssetIntention(sdkComponent.Src, hash, new CancellationTokenSource()), partitionComponent);
                    component.Promise = promise;
                    component.State = LoadingState.Loading;
                }

                eventsBuffer.Add(entity, component);
            }
            else if (component.State == LoadingState.Finished)
            {
                sdkComponent.IsDirty = false;

                Assert.IsTrue(component.Promise.Result.HasValue);

                // if promise was unsuccessful nothing to do
                StreamableLoadingResult<GltfContainerAsset> result = component.Promise.Result!.Value;
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

                entityCollidersSceneCache.Associate(in component, entity, sdkEntity);
            }
        }
    }
}
