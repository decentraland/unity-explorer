using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using SceneRuntime.Factory.WebSceneSource.Cache;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public class CachedWebJsSources : IWebJsSources
    {
        private readonly IWebJsSources origin;
        private readonly IJsSourcesCache cache;

        public CachedWebJsSources(IWebJsSources origin, IJsSourcesCache cache)
        {
            this.origin = origin;
            this.cache = cache;
        }

        public async UniTask<string> SceneSourceCode(URLAddress path, CancellationToken ct)
        {
            if (cache.TryGet(path, out string? result))
                return result!;

            string sourceCode = await origin.SceneSourceCode(path, ct);
            cache.Cache(path, sourceCode);
            return sourceCode;
        }
    }
}
