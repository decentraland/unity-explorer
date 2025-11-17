using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using SceneRuntime.Factory.JsSource;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        public UniTask<Result<DownloadedOrCachedData>> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct);
    }
}
