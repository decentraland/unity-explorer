using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Utility.Types;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.RealmNavigation.LoadingOperation
{
    public class SequentialLoadingOperation<TParams> where TParams: ILoadingOperationParams
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly ReportData reportData;

        private readonly ReactiveProperty<ILoadingOperation<TParams>?> currentOp = new (null);

        public SequentialLoadingOperation(ILoadingStatus loadingStatus, IReadOnlyList<ILoadingOperation<TParams>> operations, ReportData reportData)
        {
            this.loadingStatus = loadingStatus;
            this.reportData = reportData;
            Operations = operations;
        }

        public IReadonlyReactiveProperty<ILoadingOperation<TParams>?> CurrentOp => currentOp;

        public IReadOnlyList<ILoadingOperation<TParams>> Operations { get; }

        public ILoadingOperation<TParams>? InterruptOnOp { get; set; }

        /// <summary>
        ///     Tries to restart the whole flow for <paramref name="attemptsCount" /> times <br />
        ///     Protects from unexpected exceptions from the inner operations <br />
        ///     Reports completion only after all operations succeed.
        /// </summary>
        public virtual async UniTask<EnumResult<TaskError>> ExecuteAsync(string processName, int attemptsCount, TParams args, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

            var lastOpResult = EnumResult<TaskError>.SuccessResult();

            attemptsCount = Mathf.Max(1, attemptsCount);

            for (var attempt = 0; attempt < attemptsCount; attempt++)
            {
                foreach (ILoadingOperation<TParams> loadingOp in Operations)
                {
                    if (ct.IsCancellationRequested) return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
                    currentOp.Value = loadingOp;

                    try
                    {
                        if (loadingOp == InterruptOnOp)
                            return EnumResult<TaskError>.ErrorResult(TaskError.MessageError, $"Loading operation {loadingOp.GetType().Name} has been manually interrupted");

                        lastOpResult = await loadingOp.ExecuteAsync(args, ct);

                        if (ct.IsCancellationRequested || lastOpResult.Error is { State: TaskError.Cancelled } or { Exception: OperationCanceledException })
                            return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);

                        if (!lastOpResult.Success)
                        {
                            ReportHub.LogError(
                                reportData,
                                $"Operation failed on {processName} attempt {attempt + 1}/{attemptsCount}: {lastOpResult.AsResult().ErrorMessage}"
                            );

                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return EnumResult<TaskError>.CancelledResult(TaskError.Cancelled);
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

            if (lastOpResult.Success)
                args.Report.SetProgress(1);

            return lastOpResult;
        }
    }
}
