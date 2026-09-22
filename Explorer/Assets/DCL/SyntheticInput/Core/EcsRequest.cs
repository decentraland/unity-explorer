using Arch.Core;
using Cysharp.Threading.Tasks;
using Utility.Arch;
using Utility.Multithreading;

namespace DCL.SyntheticInput.Core
{
    /// <summary>A request component that an automation driver writes onto an entity and a system fulfils.</summary>
    public interface IEcsRequest<TResult>
    {
        /// <summary>Completed exactly once: with the outcome, or with the preemption result when a newer request replaces this one.</summary>
        UniTaskCompletionSource<TResult>? Completion { get; set; }
    }

    /// <summary>The install, complete and abandon steps shared by the synthetic input request components.</summary>
    public static class EcsRequest
    {
        /// <summary>Installs the request on the entity. Main thread only. A pending request of the same type is completed with <paramref name="preemptedResult" /> and replaced.</summary>
        public static UniTask<TResult> SendAsync<TIntent, TResult>(World world, Entity entity, TIntent request, TResult preemptedResult)
            where TIntent : struct, IEcsRequest<TResult>
        {
            MultithreadingUtility.AssertMainThread(nameof(SendAsync), true);

            if (world.TryGet(entity, out TIntent existing))
                existing.Completion?.TrySetResult(preemptedResult);

            var completion = new UniTaskCompletionSource<TResult>();
            request.Completion = completion;
            world.AddOrSet(entity, request);

            return completion.Task;
        }

        /// <summary>Removes the component before it completes the awaiter, so the continuation observes the entity without the component.</summary>
        public static void CompleteAndRemove<TIntent, TResult>(World world, Entity entity, TIntent request, TResult result)
            where TIntent : struct, IEcsRequest<TResult>
        {
            UniTaskCompletionSource<TResult>? completion = request.Completion;
            world.Remove<TIntent>(entity);
            completion?.TrySetResult(result);
        }

        /// <summary>Removes the request without completing its awaiter. Safe to call from any thread.</summary>
        public static async UniTask AbandonAsync<TIntent>(World world, Entity entity) where TIntent : struct
        {
            await UniTask.SwitchToMainThread();

            if (world.Has<TIntent>(entity))
                world.Remove<TIntent>(entity);
        }
    }
}
