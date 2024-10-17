using Cysharp.Threading.Tasks;

namespace DCL.AsyncLoadReporting
{
    public interface IAsyncLoadProcessReport
    {
        string Description { get; }

        IReadOnlyAsyncReactiveProperty<float> ProgressCounter { get; }

        UniTaskCompletionSource CompletionSource { get; }

        void SetProgress(float progress, string stepDescription);

        IAsyncLoadProcessReport CreateChildReport(float until);
    }
}
