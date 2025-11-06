using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using System.Threading;
using UnityEngine.Networking;

namespace SceneRuntime.Factory.WebSceneSource
{
    public class WebJsSources : IWebJsSources
    {
        private readonly JsCodeResolver codeContentResolver;

        public WebJsSources(JsCodeResolver codeContentResolver)
        {
            this.codeContentResolver = codeContentResolver;
        }

        public UniTask<DownloadHandler> SceneSourceCodeAsync(URLAddress path, CancellationToken ct) =>
            codeContentResolver.GetCodeContent(path, ct);
    }
}
