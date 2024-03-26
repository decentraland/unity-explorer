using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using JetBrains.Annotations;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Reporting
{
    public static class WaitForSceneReadinessExtensions
    {
        public static UniTask ToUniTask([CanBeNull] this WaitForSceneReadiness waitForSceneReadiness, CancellationToken ct) =>
            waitForSceneReadiness?.Execute(ct) ?? UniTask.CompletedTask;
    }

    public class WaitForSceneReadiness
    {
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(30);

        private readonly Vector2Int parcel;
        private readonly AsyncLoadProcessReport loadProcessReport;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;

        private bool executed;

        public WaitForSceneReadiness(Vector2Int parcel, AsyncLoadProcessReport loadProcessReport, ISceneReadinessReportQueue sceneReadinessReportQueue)
        {
            this.parcel = parcel;
            this.loadProcessReport = loadProcessReport;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
        }

        /// <summary>
        ///     Add the scene readiness report to the queue and wait for its resolution
        /// </summary>
        /// <returns></returns>
        internal async UniTask Execute(CancellationToken ct)
        {
            if (executed)
                throw new Exception(nameof(WaitForSceneReadiness) + " can be executed only once");

            executed = true;

            // Add report to the queue so it will be grabbed by the actual scene or LODs
            sceneReadinessReportQueue.Enqueue(parcel, loadProcessReport);

            try
            {
                // add timeout in case of a trouble
                await loadProcessReport.CompletionSource.Task
                                       .Timeout(TIMEOUT)
                                       .AttachExternalCancellation(ct);
            }
            catch (Exception e) { loadProcessReport.CompletionSource.TrySetException(e); }
        }
    }
}
