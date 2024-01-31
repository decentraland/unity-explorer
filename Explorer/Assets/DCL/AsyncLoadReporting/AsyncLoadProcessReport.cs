using Cysharp.Threading.Tasks;

namespace DCL.AsyncLoadReporting
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class AsyncLoadProcessReport
    {
        public UniTaskCompletionSource CompletionSource { get; }
        public IAsyncReactiveProperty<float> ProgressCounter { get; }

        public AsyncLoadProcessReport(UniTaskCompletionSource completionSource,
            IAsyncReactiveProperty<float> progressCounter)
        {
            CompletionSource = completionSource;
            ProgressCounter = progressCounter;
        }

        public static AsyncLoadProcessReport Create() =>
            new (new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0f));
    }
}
