using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.Hashing;
using DCL.SceneRunner.Scene;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using System.Threading;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    /// <summary>
    ///     Wraps the underlying typed disk cache so we don't persist <see cref="ISSDescriptorResolution.NONE"/>
    ///     entries — the no-ISS case is the majority of scenes, and a clean cache miss + 404 on revisit
    ///     is cheaper than littering the cache with empty stubs.
    /// </summary>
    public class ISSDescriptorDiskCache : IDiskCache<ISSDescriptorResolution>
    {
        private readonly IDiskCache<ISSDescriptorResolution> inner;

        public ISSDescriptorDiskCache(IDiskCache<ISSDescriptorResolution> inner)
        {
            this.inner = inner;
        }

        public UniTask<EnumResult<TaskError>> PutAsync(HashKey key, string extension, ISSDescriptorResolution data, CancellationToken token)
        {
            if (data.State == IISSDescriptor.State.None)
                return UniTask.FromResult(EnumResult<TaskError>.SuccessResult());

            return inner.PutAsync(key, extension, data, token);
        }

        public UniTask<EnumResult<Option<ISSDescriptorResolution>, TaskError>> ContentAsync(HashKey key, string extension, CancellationToken token) =>
            inner.ContentAsync(key, extension, token);

        public UniTask<EnumResult<TaskError>> RemoveAsync(HashKey key, string extension, CancellationToken token) =>
            inner.RemoveAsync(key, extension, token);
    }
}
