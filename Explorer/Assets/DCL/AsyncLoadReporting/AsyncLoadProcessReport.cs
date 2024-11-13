using Cysharp.Threading.Tasks;
using System;

namespace DCL.AsyncLoadReporting
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class AsyncLoadProcessReport
    {
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

        private readonly AsyncLoadProcessReport? parent;

        private readonly float offset;
        private readonly float until;

        private readonly IAsyncReactiveProperty<float> progressCounter;

        private readonly UniTaskCompletionSource completionSource;

        private Exception? exception;

        public IReadOnlyAsyncReactiveProperty<float> ProgressCounter => progressCounter;

        private AsyncLoadProcessReport(UniTaskCompletionSource completionSource,
            IAsyncReactiveProperty<float> progressCounter)
        {
            this.completionSource = completionSource;
            this.progressCounter = progressCounter;
            offset = 0;
            until = 1;
        }

        private AsyncLoadProcessReport(AsyncLoadProcessReport parent, float offset, float until)
        {
            completionSource = new UniTaskCompletionSource();
            progressCounter = new AsyncReactiveProperty<float>(0f);
            this.parent = parent;
            this.offset = offset;
            this.until = until;
        }

        public UniTask Task => completionSource.Task;

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
            new (this, ProgressCounter.Value, until);

        public static AsyncLoadProcessReport Create() =>
            new (new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0f));
    }
}
