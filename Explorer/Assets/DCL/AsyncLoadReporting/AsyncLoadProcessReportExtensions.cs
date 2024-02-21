using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.AsyncLoadReporting
{
    public static class AsyncLoadProcessReportExtensions
    {
        public static async UniTask PropagateProgressCounterAsync(this AsyncLoadProcessReport report, AsyncLoadProcessReport destination, CancellationToken ct,
            float offset = 0f, float until = 1f, TimeSpan? timeout = null)
        {
            while (report.ProgressCounter.Value < 1f && !ct.IsCancellationRequested)
            {
                UniTask<float> task = report.ProgressCounter.WaitAsync(ct);

                if (timeout != null)
                    task = task.Timeout(timeout.Value);

                float progress = await task;
                destination.ProgressCounter.Value = offset + (progress * (until - offset));
            }
        }


        public static async UniTask PropagateAsync(this AsyncLoadProcessReport report, AsyncLoadProcessReport destination, CancellationToken ct,
            float offset = 0f, float until = 1f, TimeSpan? timeout = null)
        {
            try
            {
                UniTask completionTask = report.CompletionSource.Task;

                if (timeout != null)
                    completionTask = completionTask.Timeout(timeout.Value);

                await UniTask.WhenAny(report.PropagateProgressCounterAsync(destination, ct, offset, until, timeout),
                    completionTask);

                destination.ProgressCounter.Value = 1f;
                destination.CompletionSource.TrySetResult();
            }
            catch (Exception e) { destination.CompletionSource.TrySetException(e); }
        }
    }
}
