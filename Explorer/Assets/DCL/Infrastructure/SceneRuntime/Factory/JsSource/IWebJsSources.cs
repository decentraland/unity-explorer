using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using SceneRuntime.Factory.JsSource;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        public UniTask<DownloadedOrCachedData> SceneSourceCodeAsync(URLAddress path, CancellationToken ct);
    }
}
