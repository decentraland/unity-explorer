using Cysharp.Threading.Tasks;
using System;

namespace DCL.ScenesDebug.ScenesConsistency.DelayedResources
{
    public class DelayedResource<T> : IDelayedResource<T>
    {
        private readonly Func<T> tryObtainResource;
        private T? resource;

        public DelayedResource(Func<T> tryObtainResource)
        {
            this.tryObtainResource = tryObtainResource;
        }

        public async UniTask<T> ResourceAsync()
        {
            while (resource == null)
            {
                resource = tryObtainResource();
                await UniTask.Yield();
            }

            return resource;
        }

        public T DangerousResource() =>
            resource ?? throw new InvalidOperationException("Resource not ready yet");
    }
}
