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
        // parcel floor height is unknown at that point. Once the scene is ready we raycast straight
        // down from well above the scene and snap onto the topmost walkable collider (Default/Floor/
        // CharacterOnly layers), so the avatar lands on the actual floor at that parcel regardless of
        // how high it sits relative to the spawn-point anchor.
        private const float LAND_ON_PARCEL_RAYCAST_UP_OFFSET = 100f;
        private const float LAND_ON_PARCEL_RAYCAST_DISTANCE = 200f;
        private const float LAND_ON_PARCEL_GROUND_CLEARANCE = 0.1f;

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
        ///     Snaps a land-on-parcel teleport down onto the scene floor, now that the scene is ready
        ///     and its colliders exist. Falls back to the precomputed position when no floor collider
        ///     is found within range.
        /// </summary>
        private static Vector3 SnapToSceneFloor(Vector3 position)
        {
            // TEMP diagnostic: dump the full vertical collider column at the landing point so we can
            // see where the real floor is relative to the anchor (and whether a ceiling/truss is in
            // the way). Remove before merge.
            Vector3 columnOrigin = position + (Vector3.up * 200f);
            LogColumn("character-only", columnOrigin, PhysicsLayers.CHARACTER_ONLY_MASK);
            LogColumn("all-layers", columnOrigin, ~0);
            ReportHub.LogProductionInfo($"[jumpin] anchor=({position.x:F2},{position.y:F2},{position.z:F2})");

            float anchorY = position.y;
            Vector3 origin = position + (Vector3.up * LAND_ON_PARCEL_RAYCAST_UP_OFFSET);

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, LAND_ON_PARCEL_RAYCAST_DISTANCE, PhysicsLayers.CHARACTER_ONLY_MASK, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                ReportHub.LogProductionInfo("[jumpin] no floor hit, keeping anchor height");
                return position;
            }

            // Pick the collider closest to the scene's spawn-point height: that's the author's intended
            // walking level, so we land on the actual floor instead of a roof/canopy high above it.
            RaycastHit best = hits[0];

            foreach (RaycastHit h in hits)
                if (Mathf.Abs(h.point.y - anchorY) < Mathf.Abs(best.point.y - anchorY))
                    best = h;

            position.y = best.point.y + LAND_ON_PARCEL_GROUND_CLEARANCE;
            ReportHub.LogProductionInfo($"[jumpin] snapped to floor y={best.point.y:F2} collider={best.collider.name}");
            return position;
        }

        // TEMP diagnostic helper. Remove before merge.
        private static void LogColumn(string label, Vector3 origin, int mask)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 400f, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, static (a, b) => a.distance.CompareTo(b.distance));
            ReportHub.LogProductionInfo($"[jumpin] column[{label}] originY={origin.y:F2} hits={hits.Length}");

            foreach (RaycastHit h in hits)
                ReportHub.LogProductionInfo($"[jumpin]   {label} y={h.point.y:F2} layer={LayerMask.LayerToName(h.collider.gameObject.layer)} collider={h.collider.name}");
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
