using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using SceneRuntime.Factory.JsSource;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public class WebJsSources : IWebJsSources
    {
        private readonly JsCodeResolver codeContentResolver;

        public WebJsSources(JsCodeResolver codeContentResolver)
        {
            this.codeContentResolver = codeContentResolver;
        }

        public async UniTask<DownloadedOrCachedData> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct) =>
            new (await codeContentResolver.GetCodeContent(path, ct));
    }
}
