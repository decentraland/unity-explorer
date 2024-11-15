using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
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

        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        internal TeleportCharacterSystem(World world, ISceneReadinessReportQueue sceneReadinessReportQueue) : base(world)
        {
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        protected override void Update(float t)
        {
            TeleportPlayerQuery(World);
            TryRemoveJustTeleportedQuery(World);
        }

        [Query]
        private void TeleportPlayer(Entity entity, CharacterController controller, ref CharacterPlatformComponent platformComponent, in PlayerTeleportIntent teleportIntent)
        {
            AsyncLoadProcessReport? loadReport = teleportIntent.LoadReport;

            if (loadReport != null)
            {
                AsyncLoadProcessReport.Status status = loadReport.GetStatus();

                switch (status.TaskStatus)
                {
                    case UniTaskStatus.Succeeded:
                        ResolveAsSuccess(entity, in teleportIntent, controller, ref platformComponent);
                        return;
                    case UniTaskStatus.Canceled:
                        ResolveAsCancelled(entity, in teleportIntent);
                        return;
                    case UniTaskStatus.Faulted:
                        ResolveAsFailure(entity, in teleportIntent, status.Exception!);
                        return;
                }
            }

            // pending cases left

            if (teleportIntent.TimedOut)
            {
                var exception = new TimeoutException("Teleport timed out");
                loadReport?.SetException(exception);
                ResolveAsFailure(entity, in teleportIntent, exception);
                return;
            }

            if (teleportIntent.CancellationToken.IsCancellationRequested)
            {
                loadReport?.SetCancelled();
                ResolveAsCancelled(entity, in teleportIntent);
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

                if (report == intent.LoadReport) // it's the same report, it was already finalized
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

        private void ResolveAsSuccess(Entity entity, in PlayerTeleportIntent teleportIntent, CharacterController controller, ref CharacterPlatformComponent platformComponent)
        {
            FinalizeQueuedLoadReport(in teleportIntent, static report => report.SetProgress(1f));

            // Teleport the character
            controller.transform.position = teleportIntent.Position;

            // Reset the current platform so we don't bounce back if we are touching the world plane
            platformComponent.CurrentPlatform = null;

            World.Remove<PlayerTeleportIntent>(entity);
            World.Add(entity, new PlayerTeleportIntent.JustTeleported(UnityEngine.Time.frameCount + COUNTDOWN_FRAMES));
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
    }
}
