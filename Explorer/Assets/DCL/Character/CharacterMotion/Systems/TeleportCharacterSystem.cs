using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Physics;
using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using System;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Mutually exclusive with <see cref="InterpolateCharacterSystem" />. <br />
    ///     This system reacts on the status of <see cref="AsyncLoadProcessReport" /> to make the appropriate changes in ECS
    /// </summary>
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    public partial class TeleportCharacterSystem : BaseUnityLoopSystem
    {
        private const int COUNTDOWN_FRAMES = 20;

        // A land-on-parcel teleport is positioned before the target scene's colliders load, so the
        // parcel floor height is unknown at that point. Once the scene is ready we probe the parcel
        // for its walkable floor: a parcel center can sit over a gap, stairs or a lower level in a
        // terraced scene, so we sample a grid across the parcel and land on the highest walkable
        // surface that's still within a step-up of the parcel's own local ground (rejecting roofs and
        // canopies far overhead).
        private const float LAND_ON_PARCEL_RAYCAST_UP_OFFSET = 100f;
        private const float LAND_ON_PARCEL_RAYCAST_DISTANCE = 200f;
        private const float LAND_ON_PARCEL_GROUND_CLEARANCE = 0.1f;

        // Offset from the parcel center, on each axis, of the outer probe samples. 5m keeps every
        // sample comfortably inside the 16m parcel (3m clear of each edge) so the avatar never settles
        // across a parcel boundary.
        private const float LAND_ON_PARCEL_SAMPLE_OFFSET = 5f;

        // A walkable surface at most this far above the parcel's lowest floor is a reachable step or
        // terrace; anything higher is treated as overhead geometry (roof, canopy, truss) and ignored.
        private const float LAND_ON_PARCEL_MAX_STEP_UP = 15f;

        // Two heights within this margin count as the same level, so ties break on proximity to center.
        private const float LAND_ON_PARCEL_LEVEL_EPSILON = 0.1f;

        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        internal TeleportCharacterSystem(World world, ISceneReadinessReportQueue sceneReadinessReportQueue) : base(world)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        protected override void Update(float t)
        {
            TeleportPlayerQuery(World);
            TryRemoveJustTeleportedQuery(World);
            TryRemoveAnimTransitionQuery(World);
        }

        [Query]
        private void TeleportPlayer(Entity entity, in PlayerTeleportIntent teleportIntent, CharacterController controller,
            CharacterPlatformComponent platformComponent, CharacterRigidTransform rigidTransform)
        {
            AsyncLoadProcessReport? loadReport = teleportIntent.AssetsResolution;

            if (teleportIntent.TimedOut)
            {
                var exception = new TimeoutException("Teleport timed out");
                loadReport?.SetException(exception);
                ResolveAsFailure(entity, in teleportIntent, exception);
                controller.detectCollisions = true;
                return;
            }

            if (teleportIntent.CancellationToken.IsCancellationRequested)
            {
                loadReport?.SetCancelled();
                ResolveAsCancelled(entity, in teleportIntent);
                controller.detectCollisions = true;
                return;
            }

            // If the position is not set, then the player might be teleported to a wrong location.
            // It is a must that TeleportPositionCalculationSystem processes the PlayerTeleportIntent before running this system,
            // especially if the scene is already loaded
            if (!teleportIntent.IsPositionSet)
            {
                // Since its a "pending" teleport, we disable collisions to prevent any undesired interaction with the scene
                controller.transform.position = teleportIntent.Position;
                controller.detectCollisions = false;
                return;
            }

            // If there are no assets to wait for, teleport immediately
            if (loadReport == null)
            {
                ResolveAsSuccess(entity, in teleportIntent, controller, platformComponent, rigidTransform);
                controller.detectCollisions = true;
            }
            else
            {
                AsyncLoadProcessReport.Status status = loadReport.GetStatus();

                switch (status.TaskStatus)
                {
                    case UniTaskStatus.Pending:
                        controller.transform.position = teleportIntent.Position;
                        // Disable collisions so the scene does not interact with the character, and we avoid possible issues at startup
                        // For example: teleport to Genesis Plaza. The dialog with the barman should not show at the spawn point
                        // See https://github.com/decentraland/unity-explorer/issues/3289 for more info
                        controller.detectCollisions = false;
                        return;
                    case UniTaskStatus.Succeeded:
                        controller.detectCollisions = true;
                        ResolveAsSuccess(entity, in teleportIntent, controller, platformComponent, rigidTransform);
                        return;
                    case UniTaskStatus.Canceled:
                        controller.detectCollisions = true;
                        ResolveAsCancelled(entity, in teleportIntent);
                        return;
                    case UniTaskStatus.Faulted:
                        controller.detectCollisions = true;
                        ResolveAsFailure(entity, in teleportIntent, status.Exception!);
                        return;
                }
            }
        }

        /// <summary>
        ///     Ensures all the queued reports are finalized according to the teleport intent
        /// </summary>
        private void FinalizeQueuedLoadReport(in PlayerTeleportIntent intent, Action<AsyncLoadProcessReport> setState)
        {
            if (!sceneReadinessReportQueue.TryDequeue(intent.Parcel, out PooledLoadReportList? loadReportList))
                return;

            using PooledLoadReportList loadReport = loadReportList!.Value;

            for (var i = 0; i < loadReport.Count; i++)
            {
                AsyncLoadProcessReport report = loadReport[i];

                if (report == intent.AssetsResolution) // it's the same report, it was already finalized
                    continue;

                setState(report);
            }
        }

        private void ResolveAsFailure(Entity entity, in PlayerTeleportIntent playerTeleportIntent, Exception exception)
        {
            // Warning: delegate allocation, it's tolerated because Teleport executes not too often
            FinalizeQueuedLoadReport(in playerTeleportIntent, report => report.SetException(exception));

            ReportHub.LogException(exception, GetReportData());
            RestoreCameraDataQuery(World);
            World.Remove<PlayerTeleportIntent>(entity);
        }

        private void ResolveAsCancelled(Entity entity, in PlayerTeleportIntent playerTeleportIntent)
        {
            FinalizeQueuedLoadReport(in playerTeleportIntent, static report => report.SetCancelled());

            RestoreCameraDataQuery(World);
            World.Remove<PlayerTeleportIntent>(entity);
        }

        private void ResolveAsSuccess(Entity playerEntity, in PlayerTeleportIntent teleportIntent, CharacterController characterController,
            CharacterPlatformComponent platformComponent, CharacterRigidTransform rigidTransform)
        {
            FinalizeQueuedLoadReport(in teleportIntent, static report => report.SetProgress(1f));

            // For a land-on-parcel teleport the floor height is only knowable now that the scene is
            // ready, so snap the avatar down onto it instead of leaving it inside or under the geometry.
            Vector3 targetPosition = teleportIntent.LandOnParcel ? SnapToSceneFloor(teleportIntent.Position) : teleportIntent.Position;

            // Only apply changes when position is actually different otherwise in-place rotation is bugged
            if (!targetPosition.Equals(characterController.transform.position))
            {
                characterController.transform.position = targetPosition;
                rigidTransform.IsGrounded = false; // teleportation is always above
            }

            // Reset the current platform so we don't bounce back if we are touching the world plane
            platformComponent.CurrentPlatform = null;

            World.Remove<PlayerTeleportIntent>(playerEntity);
            World.Add(playerEntity, new PlayerTeleportIntent.JustTeleported(UnityEngine.Time.frameCount + COUNTDOWN_FRAMES, teleportIntent.Parcel));
        }

        /// <summary>
        ///     Snaps a land-on-parcel teleport onto the parcel's walkable floor, now that the scene is
        ///     ready and its colliders exist. Probes a grid across the parcel and picks the highest
        ///     walkable surface within a step-up of the parcel's local ground, so the avatar lands on an
        ///     elevated terrace/deck rather than the lower plane beneath it. Falls back to the
        ///     precomputed position when no floor collider is found anywhere in the parcel.
        /// </summary>
        private static Vector3 SnapToSceneFloor(Vector3 position)
        {
            // The scene container is moved from its loading position (Mordor, ~-10000) to its real
            // position on the same frame the readiness report resolves, and physics runs in manual mode
            // with auto-sync off. On the teleport-resolution frame the per-frame sync may be skipped, so
            // force one here to guarantee the raycasts query the colliders' current poses.
            Physics.SyncTransforms();

            // Sample a 3x3 grid spanning the parcel and collect every walkable hit under each column.
            // The parcel center may sit over a gap or a lower level, so the floor we want is often only
            // found off-center (e.g. an elevated step along one edge in a terraced scene).
            Span<float> offsets = stackalloc float[] { -LAND_ON_PARCEL_SAMPLE_OFFSET, 0f, LAND_ON_PARCEL_SAMPLE_OFFSET };

            var hasHit = false;
            var lowestFloor = float.PositiveInfinity;

            foreach (float dx in offsets)
            foreach (float dz in offsets)
            {
                Vector3 origin = new (position.x + dx, position.y + LAND_ON_PARCEL_RAYCAST_UP_OFFSET, position.z + dz);

                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, LAND_ON_PARCEL_RAYCAST_DISTANCE, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore);

                foreach (RaycastHit h in hits)
                {
                    hasHit = true;
                    if (h.point.y < lowestFloor) lowestFloor = h.point.y;
                }
            }

            if (!hasHit) return position; // nothing to stand on in this parcel; keep the anchored height

            // Anything within a step-up of the parcel's lowest floor is walkable; higher hits are roofs.
            float ceiling = lowestFloor + LAND_ON_PARCEL_MAX_STEP_UP;

            var bestY = float.NegativeInfinity;
            Vector3 bestPoint = position;
            var bestCenterDistSq = float.PositiveInfinity;

            foreach (float dx in offsets)
            foreach (float dz in offsets)
            {
                Vector3 origin = new (position.x + dx, position.y + LAND_ON_PARCEL_RAYCAST_UP_OFFSET, position.z + dz);

                RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, LAND_ON_PARCEL_RAYCAST_DISTANCE, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore);

                foreach (RaycastHit h in hits)
                {
                    if (h.point.y > ceiling) continue; // overhead geometry, not a walkable step

                    float centerDistSq = (dx * dx) + (dz * dz);

                    // Prefer the highest walkable surface; on ties prefer the sample nearest the center
                    // so flat parcels land the avatar in the middle rather than at an arbitrary edge.
                    bool higher = h.point.y > bestY + LAND_ON_PARCEL_LEVEL_EPSILON;
                    bool sameLevelButCentred = h.point.y >= bestY - LAND_ON_PARCEL_LEVEL_EPSILON && centerDistSq < bestCenterDistSq;

                    if (higher || sameLevelButCentred)
                    {
                        bestY = h.point.y;
                        bestPoint = h.point;
                        bestCenterDistSq = centerDistSq;
                    }
                }
            }

            return new Vector3(bestPoint.x, bestPoint.y + LAND_ON_PARCEL_GROUND_CLEARANCE, bestPoint.z);
        }

        [Query]
        private void RestoreCameraData(CameraSamplingData cameraSamplingData, in CameraComponent cameraComponent)
        {
            ScenesPartitioningUtils.UpdatePartitionDiscreteData(cameraSamplingData, cameraComponent.Camera.transform);
        }

        [Query]
        private void TryRemoveJustTeleported(Entity entity, PlayerTeleportIntent.JustTeleported justTeleported)
        {
            if (justTeleported.ExpireFrame <= UnityEngine.Time.frameCount)
                World.Remove<PlayerTeleportIntent.JustTeleported>(entity);
        }

        [Query]
        [All(typeof(DisableAnimationTransitionOnTeleport))]
        [None(typeof(PlayerTeleportIntent))]
        private void TryRemoveAnimTransition(Entity entity, DisableAnimationTransitionOnTeleport disableAnim)
        {
            if (disableAnim.ExpireFrame <= UnityEngine.Time.frameCount)
                World.Remove<DisableAnimationTransitionOnTeleport>(entity);
        }
    }
}
