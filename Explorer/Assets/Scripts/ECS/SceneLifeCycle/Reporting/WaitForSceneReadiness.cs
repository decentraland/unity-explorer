﻿using Cysharp.Threading.Tasks;
using DCL.Utilities;
using System;
using UnityEngine;
using Utility.Types;

namespace ECS.SceneLifeCycle.Reporting
{
    public static class WaitForSceneReadinessExtensions
    {
        public static UniTask<EnumResult<TaskError>> ToUniTask(this WaitForSceneReadiness? waitForSceneReadiness) =>
            waitForSceneReadiness?.ExecuteAsync() ?? UniTask.FromResult(EnumResult<TaskError>.SuccessResult());
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
        internal UniTask<EnumResult<TaskError>> ExecuteAsync()
        {
            if (executed)
                throw new Exception(nameof(WaitForSceneReadiness) + " can be executed only once");

            executed = true;

            // Add report to the queue so it will be grabbed by the actual scene or LODs
            sceneReadinessReportQueue.Enqueue(parcel, loadProcessReport);

            return loadProcessReport.WaitUntilFinishedAsync();
        }
    }
}
