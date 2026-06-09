using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using Utility;
using System;
using System.Collections.Generic;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    public partial class TeleportPositionCalculationSystem : BaseUnityLoopSystem
    {


        private readonly ILandscape landscape;

        private SingleInstanceEntity? cameraCached;
        private SingleInstanceEntity cameraEntity => cameraCached ??= World.CacheCamera();

        public TeleportPositionCalculationSystem(World world, ILandscape landscape) : base(world)
        {
            this.landscape = landscape;
        }

        protected override void Update(float t)
        {
            CalculateTeleportPositionQuery(World);
        }

        [Query]
        private void CalculateTeleportPosition(in Entity playerEntity, ref PlayerTeleportIntent teleportIntent)
        {
            if (teleportIntent.IsPositionSet) return;

            SceneEntityDefinition? sceneDef = teleportIntent.SceneDef;
            Vector2Int parcel = teleportIntent.Parcel;

            if (sceneDef == null)
            {
                Vector3 targetWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation();
                teleportIntent.Position = targetWorldPosition.WithTerrainOffset(landscape.GetHeight(targetWorldPosition.x, targetWorldPosition.z));
            }
            else if (TeleportUtils.IsRoad(sceneDef.metadata.OriginalJson.AsSpan())) { teleportIntent.Position = ParcelMathHelper.GetPositionByParcelPosition(parcel).WithErrorCompensation(); }
            else if (teleportIntent.LandOnParcel)
            {
                // Land at the requested parcel itself rather than the scene's spawn point
                // (e.g. jumping into an event located at a specific parcel of a multi-parcel scene).
                // Aim at the parcel center instead of its base corner: the corner lies on the parcel
                // boundary, where settling tips the avatar into the neighbouring parcel.
                const float HALF_PARCEL_SIZE = ParcelMathHelper.PARCEL_SIZE / 2f;
                Vector3 targetWorldPosition = ParcelMathHelper.GetPositionByParcelPosition(parcel)
                                              + new Vector3(HALF_PARCEL_SIZE, 0f, HALF_PARCEL_SIZE);

                // Keep the landing inside the scene's parcels; falls back to the parcel base if not.
                ValidateTeleportPosition(ref targetWorldPosition, parcel, sceneDef);

                // Derive the height from the scene's spawn point, not the Genesis terrain: a built
                // scene's floor rarely sits at terrain height, so a terrain offset would bury the avatar.
                (Vector3 spawnTarget, _) = TeleportUtils.PickTargetWithOffset(sceneDef, parcel);
                targetWorldPosition.y = spawnTarget.y;

                teleportIntent.Position = targetWorldPosition;
            }
            else
            {
                (Vector3 targetWorldPosition, Vector3? cameraTarget) = TeleportUtils.PickTargetWithOffset(sceneDef, parcel);

                var originalTargetPosition = targetWorldPosition;

                if (!ValidateTeleportPosition(ref targetWorldPosition, parcel, sceneDef))
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Invalid teleport position: {originalTargetPosition}. Adjusted to: {targetWorldPosition}");

                teleportIntent.Position = targetWorldPosition;

                if (cameraTarget != null)
                {
                    World?.AddOrGet(cameraEntity, new CameraLookAtIntent(cameraTarget.Value, targetWorldPosition));
                    World?.AddOrGet(playerEntity, new PlayerLookAtIntent(cameraTarget.Value, targetWorldPosition));
                }
            }

            teleportIntent.IsPositionSet = true;

            // The component needs to be re-applied to the entity to ensure that changes are properly propagated
            // within the ECS structure. Without this, other systems may receive an outdated version of the component.
            World!.Set(playerEntity, teleportIntent);
        }

        private bool ValidateTeleportPosition(ref Vector3 targetPosition, Vector2Int targetParcel, SceneEntityDefinition sceneDefinition)
        {
            IReadOnlyList<Vector2Int> sceneParcels = sceneDefinition.metadata.scene.DecodedParcels;

            foreach (var sceneParcel in sceneParcels)
                if (ParcelMathHelper.CalculateCorners(sceneParcel).Contains(targetPosition))
                    return true;

            // Invalid position, use the target parcel to compute a valid one
            targetPosition = ParcelMathHelper.GetPositionByParcelPosition(targetParcel).WithErrorCompensation();
            return false;
        }




    }
}
