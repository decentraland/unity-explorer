using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterMotion.Systems;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.PrimitiveColliders.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Utility;
using static Unity.Mathematics.math;

namespace ECS.Unity.SceneBoundsChecker
{
    /// <summary>
    /// This system is the replacement of another system that disabled
    /// colliders that went out of bounds of the scenes they belong to. It had
    /// a significant per-frame cost that this system does not. This system
    /// does a different thing: it enables colliders in scenes that the player
    /// is overlapping with while disabling all others.
    /// </summary>
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public sealed partial class DisableSceneCollidersSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;
        private readonly float playerRadius;
        private readonly List<Vector2Int> overlappedParcels;
        private readonly List<ISceneFacade> overlappedScenes;
        private readonly IScenesCache sceneCache;

        private DisableSceneCollidersSystem(World world, Entity playerEntity,
            IScenesCache sceneCache) : base(world)
        {
            this.playerEntity = playerEntity;
            this.sceneCache = sceneCache;

            // The worst case is when the player stands on a corner between
            // four parcels that each belong to a different scene.
            overlappedParcels = new List<Vector2Int>(4);
            overlappedScenes = new List<ISceneFacade>(4);

            var playerObject = world.Get<ICharacterObject>(playerEntity);
            playerRadius = playerObject.Controller.radius;

            // There are three cases to consider: player moving around, a scene
            // loading into a parcel the player is standing in, and the player
            // teleporting to a world or back.
            sceneCache.SceneAdded += OnSceneLoaded;
            sceneCache.ScenesCleared += OnAllScenesUnloaded;
        }

        protected override void Update(float t)
        {
            var playerTransform = World.Get<CharacterTransform>(playerEntity);

            RectInt overlappedRect = ParcelMathHelper.PositionToParcelRect(
                float3(playerTransform.Position).xz, playerRadius);

            for (int i = overlappedParcels.Count - 1; i >= 0; i--)
            {
                Vector2Int parcel = overlappedParcels[i];

                if (overlappedRect.Contains(parcel))
                    continue;

                overlappedParcels.RemoveAtSwapBack(i);

                if (!sceneCache.TryGetByParcel(parcel, out ISceneFacade scene))
                    continue;

                overlappedScenes.RemoveSwapBack(scene);

                // Duplicates in the overlappedScenes list are used as a kind
                // of reference counting. Consider the case where you cross a
                // parcel boundary inside a scene. As you enter the next
                // parcel, you add the scene a second time and as you exit the
                // previous parcel you remove it, thus avoiding enabling or
                // disabling any colliders needlessly.
                if (overlappedScenes.Contains(scene))
                    continue;

                scene.SceneStateProvider.IsOverlapped = false;
                var world = scene.EcsExecutor.World;
                DisablePrimitiveCollidersQuery(world);
                DisableGltfCollidersQuery(world);
            }

            for (int y = overlappedRect.yMin; y < overlappedRect.yMax; y++)
            for (int x = overlappedRect.xMin; x < overlappedRect.xMax; x++)
            {
                Vector2Int parcel = new Vector2Int(x, y);

                if (overlappedParcels.Contains(parcel))
                    continue;

                overlappedParcels.Add(parcel);

                if (!sceneCache.TryGetByParcel(parcel, out ISceneFacade scene))
                    continue;

                bool alreadyContains = overlappedScenes.Contains(scene);
                overlappedScenes.Add(scene);

                if (alreadyContains)
                    continue;

                scene.SceneStateProvider.IsOverlapped = true;
                var world = scene.EcsExecutor.World;
                EnablePrimitiveCollidersQuery(world);
                EnableGltfCollidersQuery(world);
            }
        }

        private void OnAllScenesUnloaded() =>
            overlappedScenes.Clear();

        private void OnSceneLoaded(ISceneFacade loadedScene)
        {
            foreach (Vector2Int parcel in overlappedParcels)
            {
                if (sceneCache.TryGetByParcel(parcel, out ISceneFacade scene)
                    && scene == loadedScene)
                {
                    overlappedScenes.Add(loadedScene);
                    loadedScene.SceneStateProvider.IsOverlapped = true;
                }
            }
        }

        [Query]
        private static void DisableGltfColliders(
            ref GltfContainerComponent component) =>
            ForceActiveBySceneBounds(ref component, false);

        [Query]
        private static void DisablePrimitiveColliders(
            ref PrimitiveColliderComponent component) =>
            ForceActiveBySceneBounds(ref component, false);

        [Query]
        private static void EnableGltfColliders(
            ref GltfContainerComponent component) =>
            ForceActiveBySceneBounds(ref component, true);

        [Query]
        private static void EnablePrimitiveColliders(
            ref PrimitiveColliderComponent component) =>
            ForceActiveBySceneBounds(ref component, true);

        private static void ForceActiveBySceneBounds(
            ref PrimitiveColliderComponent component, bool value) =>
            component.SDKCollider.ForceActiveBySceneBounds(value);

        private static void ForceActiveBySceneBounds(
            ref GltfContainerComponent component, bool value)
        {
            if (component.State != LoadingState.Finished)
                return;

            var asset = component.Promise.Result?.Asset;

            if (asset == null)
                return;

            ForceActiveBySceneBounds(asset.InvisibleColliders, value);

            if (asset.DecodedVisibleSDKColliders != null)
                ForceActiveBySceneBounds(asset.DecodedVisibleSDKColliders,
                    value);
        }

        private static void ForceActiveBySceneBounds(
            List<SDKCollider> colliders, bool value)
        {
            for (var i = 0; i < colliders.Count; i++)
            {
                SDKCollider collider = colliders[i];
                collider.ForceActiveBySceneBounds(value);
                colliders[i] = collider;
            }
        }
    }
}
