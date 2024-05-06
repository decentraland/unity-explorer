using Cysharp.Threading.Tasks;
using JetBrains.Annotations;

namespace DCL.AsyncLoadReporting
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class AsyncLoadProcessReport
    {
        public UniTaskCompletionSource CompletionSource { get; }
        public IAsyncReactiveProperty<float> ProgressCounter { get; }

        [CanBeNull] private readonly AsyncLoadProcessReport parent;
        private readonly float offset;
        private readonly float until;


        public void SetProgress(float progress)
        {
            ProgressCounter.Value = progress;
            if (parent != null)
                parent?.SetProgress(offset + ProgressCounter.Value * (until - offset));

            if (ProgressCounter.Value >= 1f)
                CompletionSource.TrySetResult();
        }

        public AsyncLoadProcessReport CreateChildReport(float until)
        {
            return new AsyncLoadProcessReport(this, ProgressCounter.Value, until);
        }

        
        public AsyncLoadProcessReport(UniTaskCompletionSource completionSource,
            IAsyncReactiveProperty<float> progressCounter)
        {
            CompletionSource = completionSource;
            ProgressCounter = progressCounter;
            offset = 0;
            until = 1;
        }

        private AsyncLoadProcessReport(AsyncLoadProcessReport parent, float offset, float until)
        {
            CompletionSource = new UniTaskCompletionSource();
            ProgressCounter = new AsyncReactiveProperty<float>(0f);
            this.parent = parent;
            this.offset = offset;
            this.until = until;
        }

        public static AsyncLoadProcessReport Create() =>
            new (new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0f));
    }
}
