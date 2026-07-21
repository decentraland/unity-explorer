using Arch.Core;
using Cysharp.Threading.Tasks;
using Utility.Arch;

namespace DCL.McpServer.Core
{
    /// <summary>
    ///     A request component written onto an entity by an MCP tool and fulfilled by a consuming system.
    ///     The shared install/complete/abandon choreography lives in <see cref="McpRequest" />.
    /// </summary>
    public interface IMcpEcsRequest<TResult>
    {
        /// <summary>
        ///     Completed exactly once: with the outcome by the fulfilling system, or with a preemption result
        ///     when a newer request replaces this one before it is fulfilled.
        /// </summary>
        UniTaskCompletionSource<TResult>? Completion { get; set; }
    }

    /// <summary>
    ///     The request/response choreography shared by MCP intent components: a tool installs the request
    ///     (preempting a pending one), the consuming system completes and removes it, and the tool-side timeout
    ///     abandons a request the simulation never picked up.
    /// </summary>
    public static class McpRequest
    {
        /// <summary>
        ///     Installs the request on the entity and returns the task its consuming system will complete.
        ///     A pending request of the same type is preempted: its awaiter is released with
        ///     <paramref name="preemptedResult" /> before the component is replaced. Main thread only.
        /// </summary>
        public static UniTask<TResult> SendAsync<TIntent, TResult>(World world, Entity entity, TIntent request, TResult preemptedResult)
            where TIntent : struct, IMcpEcsRequest<TResult>
        {
            if (world.TryGet(entity, out TIntent existing))
                existing.Completion?.TrySetResult(preemptedResult);

            var completion = new UniTaskCompletionSource<TResult>();
            request.Completion = completion;
            world.AddOrSet(entity, request);

            return completion.Task;
        }

        /// <summary>
        ///     Removes the request component, then releases its awaiter with <paramref name="result" />.
        ///     Takes the request by copy and removes before completing, so the awaiter's continuation observes
        ///     the entity without the component; the caller must be done with any refs into the entity, as the
        ///     removal is a structural change.
        /// </summary>
        public static void CompleteAndRemove<TIntent, TResult>(World world, Entity entity, TIntent request, TResult result)
            where TIntent : struct, IMcpEcsRequest<TResult>
        {
            UniTaskCompletionSource<TResult>? completion = request.Completion;
            world.Remove<TIntent>(entity);
            completion?.TrySetResult(result);
        }

        /// <summary>Drops a request the simulation never completed (tool-side timeout). Safe to call from any thread.</summary>
        public static async UniTask AbandonAsync<TIntent>(World world, Entity entity) where TIntent : struct
        {
            await UniTask.SwitchToMainThread();

            if (world.Has<TIntent>(entity))
                world.Remove<TIntent>(entity);
        }
    }
}
