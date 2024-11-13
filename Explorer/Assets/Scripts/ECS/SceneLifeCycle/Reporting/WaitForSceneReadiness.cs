using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using System;
using UnityEngine;

namespace ECS.SceneLifeCycle.Reporting
{
    public static class WaitForSceneReadinessExtensions
    {
        public static UniTask ToUniTask(this WaitForSceneReadiness? waitForSceneReadiness) =>
            waitForSceneReadiness?.ExecuteAsync() ?? UniTask.CompletedTask;
    }

    public class WaitForSceneReadiness
    {
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
        internal async UniTask ExecuteAsync()
        {
            if (executed)
                throw new Exception(nameof(WaitForSceneReadiness) + " can be executed only once");

            executed = true;

            // Add report to the queue so it will be grabbed by the actual scene or LODs
            sceneReadinessReportQueue.Enqueue(parcel, loadProcessReport);

            await loadProcessReport.Task;
        }
    }
}
