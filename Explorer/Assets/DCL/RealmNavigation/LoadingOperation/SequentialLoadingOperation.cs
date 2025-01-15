using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UserInAppInitializationFlow;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Types;

namespace DCL.RealmNavigation.LoadingOperation
{
    public class SequentialLoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly ReportData reportData;

        public IReadOnlyList<ILoadingOperation<TParams>> Operations { get; }

        public ILoadingOperation<TParams>? InterruptOnOp { get; set; }

        public SequentialLoadingOperation(ILoadingStatus loadingStatus, IReadOnlyList<ILoadingOperation<TParams>> operations, ReportData reportData)
        {
            this.loadingStatus = loadingStatus;
            this.reportData = reportData;
            Operations = operations;
        }

        public async UniTask<EnumResult<TaskError>> ExecuteAsync(string processName, int attemptsCount, TParams args, CancellationToken ct)
        {
            var lastOpResult = EnumResult<TaskError>.SuccessResult();

            attemptsCount = Mathf.Max(1, attemptsCount);

            for (var attempt = 0; attempt < attemptsCount; attempt++)
            {
                foreach (ILoadingOperation<TParams> loadingOp in Operations)
                {
                    try
                    {
                        if (loadingOp == InterruptOnOp)
                            return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, $"Loading operation {loadingOp.GetType().Name} has been manually interrupted");

                        lastOpResult = await loadingOp.ExecuteAsync(args, ct);

                        if (!lastOpResult.Success)
                        {
                            ReportHub.LogError(
                                reportData,
                                $"Operation failed on {processName} attempt {attempt + 1}/{attemptsCount}: {lastOpResult.AsResult().ErrorMessage}"
                            );

                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        lastOpResult = EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, $"Unhandled exception on {processName} attempt {attempt + 1}/{attemptsCount}: {e}");
                        ReportHub.LogError(reportData, lastOpResult.AsResult().ErrorMessage!);
                        break;
                    }
                }

                if (lastOpResult.Success)
                    break;

                if (ct.IsCancellationRequested)
                {
                    lastOpResult = EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
                    break;
                }
            }

            // The final progress should be always "1"
            args.Report.SetProgress(loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));

            return lastOpResult;
        }
    }
}
