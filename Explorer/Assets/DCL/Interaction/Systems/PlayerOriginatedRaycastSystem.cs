using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Physics;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Raycast.Components;
using DCL.Interaction.Utility;
using DCL.InWorldCamera;
using ECS.Abstract;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Interaction.Systems
{
    /// <summary>
    ///     <para>
    ///         Raycasts from the player camera and prepares data that will be consumed by other systems
    ///     </para>
    ///     <para>
    ///         When a <see cref="SyntheticPointerInput" /> aim is posted for this frame, the ray is built from the
    ///         camera through that world point instead of the cursor, so automation drivers steer the same pipeline
    ///     </para>
    ///     <para>
    ///         Runs in the global world
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    [LogCategory(ReportCategory.INPUT)]
    public partial class PlayerOriginatedRaycastSystem : BaseUnityLoopSystem
    {
        /// <summary>The reticle raycast length in production; diagnostics that re-trace the ray must use the same reach.</summary>
        public const float MAX_RAYCAST_DISTANCE = 100f;

        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly float maxRaycastDistance;
        private readonly PlayerInteractionEntity playerInteractionEntity;

        internal PlayerOriginatedRaycastSystem(World world,
            IEntityCollidersGlobalCache collidersGlobalCache,
            PlayerInteractionEntity playerInteractionEntity,
            float maxRaycastDistance) : base(world)
        {
            this.collidersGlobalCache = collidersGlobalCache;
            this.playerInteractionEntity = playerInteractionEntity;
            this.maxRaycastDistance = maxRaycastDistance;
        }

        protected override void Update(float t)
        {
            RaycastFromCameraQuery(World);
        }

        [Query]
        private void RaycastFromCamera(Entity entity, ref CameraComponent camera, in CursorComponent cursorComponent)
        {
            ref PlayerOriginRaycastResultForSceneEntities raycastResultForSceneEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForSceneEntities;
            ref PlayerOriginRaycastResultForGlobalEntities raycastResultForGlobalEntities = ref playerInteractionEntity.PlayerOriginRaycastResultForGlobalEntities;
            GlobalColliderGlobalEntityInfo? previousGlobalEntity = raycastResultForGlobalEntities.GetEntityInfo();

            try
            {

                if (cursorComponent.CursorState == CursorState.Panning || World.Has<InWorldCameraComponent>(entity))
                {
                    raycastResultForSceneEntities.Reset();
                    raycastResultForSceneEntities.ClearSyntheticAim();
                    raycastResultForGlobalEntities.Reset();
                    return;
                }

                Ray ray;
                Vector3? consumedSyntheticAim = null;
                SyntheticPointerInput syntheticInput = playerInteractionEntity.SyntheticPointerInput;

                // A stale aim (posted on a frame this system did not run) must not steer this frame's ray.
                if (syntheticInput is { IsPostedThisFrame: true, AimPoint: { } syntheticAimPoint })
                {
                    consumedSyntheticAim = syntheticAimPoint;

                    Vector3 origin = camera.Camera.transform.position;
                    ray = new Ray(origin, syntheticAimPoint - origin);
                }
                else
                    ray = CreateRay(in camera, in cursorComponent);

                // we are interested in one hit only
                bool hasHit = Physics.Raycast(ray, out RaycastHit hitInfo, maxRaycastDistance, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK);

                raycastResultForSceneEntities.SetRay(ray, consumedSyntheticAim);
                raycastResultForGlobalEntities.SetRay(ray);

                if (hasHit)
                {
                    float distance = camera.Mode == CameraMode.FirstPerson ? hitInfo.distance : Vector3.Distance(hitInfo.point, camera.PlayerFocus.position);

                    if (collidersGlobalCache.TryGetSceneEntity(hitInfo.collider, out GlobalColliderSceneEntityInfo sceneEntityInfo))
                    {
                        Vector3? playerPosition = playerInteractionEntity.PlayerPosition;
                        float? playerDistance = null;

                        if (playerPosition != null)
                            playerDistance = Vector3.Distance(hitInfo.point, (Vector3)playerPosition);

                        raycastResultForSceneEntities.SetupHit(hitInfo, sceneEntityInfo, distance, playerDistance);
                    }
                    else
                        raycastResultForSceneEntities.Reset();

                    if (collidersGlobalCache.TryGetGlobalEntity(hitInfo.collider, out GlobalColliderGlobalEntityInfo globalEntityInfo))
                        raycastResultForGlobalEntities.SetupHit(hitInfo, globalEntityInfo, distance);
                    else
                        raycastResultForGlobalEntities.Reset();
                }
                else
                {
                    raycastResultForSceneEntities.Reset();
                    raycastResultForGlobalEntities.Reset();
                }
            }
            finally
            {
                // Update HoveredComponent
                Entity? newGlobalEntity = raycastResultForGlobalEntities.GetEntityInfo()?.EntityReference;
                if(previousGlobalEntity.HasValue && previousGlobalEntity.Value.EntityReference != newGlobalEntity)
                    World.Remove<HoveredComponent>(previousGlobalEntity.Value.EntityReference);

                // Add hover to newly hit entity (only global entities, not scene entities)
                // Scene entities don't need hover markers since visual highlighting is only used for avatars
                if(newGlobalEntity.HasValue && (!previousGlobalEntity.HasValue || previousGlobalEntity.Value.EntityReference != newGlobalEntity))
                    World.Add<HoveredComponent>(newGlobalEntity.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Ray CreateRay(in CameraComponent cameraComponent, in CursorComponent cursorComponent) =>
            cameraComponent.Camera.ScreenPointToRay(cursorComponent.CursorState != CursorState.Free
                ? new Vector3(cameraComponent.Camera.pixelWidth / 2f, cameraComponent.Camera.pixelHeight / 2f, 0)
                : cursorComponent.Position);
    }
}
