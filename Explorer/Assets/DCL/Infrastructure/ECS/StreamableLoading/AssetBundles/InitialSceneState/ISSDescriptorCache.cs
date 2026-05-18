using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     In-memory cache of resolved <see cref="ISSDescriptor"/> instances keyed by
    ///     <see cref="GetISSDescriptor"/>. The descriptor for a given scene id doesn't change
    ///     during the runtime session, so we hold results indefinitely — both LOD-path and
    ///     SDK-runtime triggers hit the same entry.
    /// </summary>
    public class ISSDescriptorCache : IStreamableCache<ISSDescriptor, GetISSDescriptor>
    {
        /// <summary>
        ///     Static singleton — ISS descriptors are global per-scene-id, and consumers span many
        ///     asmdefs and constructor flows. Avoids threading the instance through every plugin DI chain.
        /// </summary>
        public static readonly ISSDescriptorCache INSTANCE = new ();

        private readonly Dictionary<string, ISSDescriptor> resolvedBySceneId = new ();

        public IDictionary<IntentionsComparer<GetISSDescriptor>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<ISSDescriptor>>> OngoingRequests { get; } =
            new Dictionary<IntentionsComparer<GetISSDescriptor>.SourcedIntentionId, UniTaskCompletionSource<OngoingRequestResult<ISSDescriptor>>>();

        public IDictionary<IntentionsComparer<GetISSDescriptor>.SourcedIntentionId, StreamableLoadingResult<ISSDescriptor>?> IrrecoverableFailures { get; } =
            new Dictionary<IntentionsComparer<GetISSDescriptor>.SourcedIntentionId, StreamableLoadingResult<ISSDescriptor>?>();

        public bool TryGet(in GetISSDescriptor key, out ISSDescriptor asset) =>
            resolvedBySceneId.TryGetValue(key.SceneId, out asset!);

        public void Add(in GetISSDescriptor key, ISSDescriptor asset)
        {
            resolvedBySceneId[key.SceneId] = asset;
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }

        public void Dispose()
        {
            resolvedBySceneId.Clear();
        }
    }
}
