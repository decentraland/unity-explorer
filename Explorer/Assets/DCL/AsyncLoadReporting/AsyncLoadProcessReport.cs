using Cysharp.Threading.Tasks;

namespace DCL.AsyncLoadReporting
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class AsyncLoadProcessReport : IAsyncLoadProcessReport
    {
        private readonly AsyncLoadProcessReport? parent;

        private readonly float offset;
        private readonly float until;

        private readonly IAsyncReactiveProperty<float> progressCounter;
        private string description = string.Empty;

        public UniTaskCompletionSource CompletionSource { get; }

        public string Description => description;

        public IReadOnlyAsyncReactiveProperty<float> ProgressCounter => progressCounter;

        private AsyncLoadProcessReport(UniTaskCompletionSource completionSource,
            IAsyncReactiveProperty<float> progressCounter)
        {
            CompletionSource = completionSource;
            this.progressCounter = progressCounter;
            offset = 0;
            until = 1;
        }

        private AsyncLoadProcessReport(AsyncLoadProcessReport parent, float offset, float until)
        {
            CompletionSource = new UniTaskCompletionSource();
            progressCounter = new AsyncReactiveProperty<float>(0f);
            this.parent = parent;
            this.offset = offset;
            this.until = until;
        }

        public void SetProgress(float progress, string stepDescription)
        {
            description = stepDescription;
            progressCounter.Value = progress;

            parent?.SetProgress(offset + (progressCounter.Value * (until - offset)), stepDescription);

            if (progressCounter.Value >= 1f)
                CompletionSource.TrySetResult();
        }

        public IAsyncLoadProcessReport CreateChildReport(float until) =>
            new AsyncLoadProcessReport(this, ProgressCounter.Value, until);

        public static AsyncLoadProcessReport Create() =>
            new (new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0f));
    }
}
