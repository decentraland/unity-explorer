using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.AsyncLoadReporting
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class AsyncLoadProcessReport
    {
        private readonly AsyncLoadProcessReport? parent;

        private readonly float offset;
        private readonly float until;

        private readonly IAsyncReactiveProperty<float> progressCounter;

        private readonly UniTaskCompletionSource completionSource;
        private readonly CancellationToken cancellationToken;

        private Exception? exception;

        public IReadOnlyAsyncReactiveProperty<float> ProgressCounter => progressCounter;

        private AsyncLoadProcessReport(AsyncLoadProcessReport? parent, float offset, float until, CancellationToken ct)
        {
            completionSource = new UniTaskCompletionSource();
            progressCounter = new AsyncReactiveProperty<float>(0f);

            this.parent = parent;
            this.offset = offset;
            this.until = until;

            cancellationToken = ct;

            ct.RegisterWithoutCaptureExecutionContext(SetCancelled);
        }

        /// <summary>
        ///     Translates internals that can throw exceptions into a result free from exceptions
        /// </summary>
        /// <returns></returns>
        public async UniTask<Status> WaitUntilFinished()
        {
            try { await completionSource.Task; }
            catch (Exception e) { return new Status(e, completionSource.UnsafeGetStatus()); }

            return new Status(null, completionSource.UnsafeGetStatus());
        }

        public Status GetStatus() =>
            new (exception, completionSource.UnsafeGetStatus());

        public void SetProgress(float progress)
        {
            progressCounter.Value = progress;

            parent?.SetProgress(offset + (progressCounter.Value * (until - offset)));

            if (progressCounter.Value >= 1f)
                completionSource.TrySetResult();
        }

        public void SetException(Exception e)
        {
            exception = e;

            completionSource.TrySetException(e);
            parent?.SetException(e);
        }

        public void SetCancelled()
        {
            completionSource.TrySetCanceled();
            parent?.SetCancelled();
        }

        public AsyncLoadProcessReport CreateChildReport(float until) =>
            new (this, ProgressCounter.Value, until, cancellationToken);

        public static AsyncLoadProcessReport Create(CancellationToken ct) =>
            new (null, 0, 1, ct);

        public readonly struct Status
        {
            public readonly Exception? Exception;
            public readonly UniTaskStatus TaskStatus;

            public Status(Exception? exception, UniTaskStatus taskStatus)
            {
                Exception = exception;
                TaskStatus = taskStatus;
            }
        }
    }
}
