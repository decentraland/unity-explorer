using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.ClearScript;

namespace SceneRuntime.Apis
{
    public class JSTaskResolverResetable
    {
        private AutoResetUniTaskCompletionSource source;

        public UniTask Task => source.Task;

        public void Reset()
        {
            source = AutoResetUniTaskCompletionSource.Create();
        }

        [UsedImplicitly]
        public void Completed()
        {
            source.TrySetResult();
        }

        [UsedImplicitly]
        public void Reject(string message)
        {
            source.TrySetException(new ScriptEngineException(message));
        }
    }
}
