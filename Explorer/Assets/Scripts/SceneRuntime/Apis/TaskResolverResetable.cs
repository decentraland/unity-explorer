using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using System;

namespace SceneRuntime.Apis
{
    public class TaskResolverResetable
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
            source.TrySetException(new Exception(message));
        }
    }
}
