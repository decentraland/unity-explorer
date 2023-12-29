using Cysharp.Threading.Tasks;

namespace SceneRunner.Scene
{
    /// <summary>
    ///     Via this class the system that runs in the scene world can notify about its [visual] readiness to the upper layer
    /// </summary>
    public class SceneReadinessReport
    {
        public UniTaskCompletionSource CompletionSource { get; }

        public SceneReadinessReport(UniTaskCompletionSource completionSource)
        {
            CompletionSource = completionSource;
        }
    }
}
