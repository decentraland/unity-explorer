using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.Utility.Types;
using SceneRuntime.Factory.JsSource;
using System.Threading;
using DownloadedCodeContent = UnityEngine.Networking.DownloadHandler;

namespace SceneRuntime.Factory.WebSceneSource
{
    public class WebJsSources : IWebJsSources
    {
        private readonly JsCodeResolver codeContentResolver;

        public WebJsSources(JsCodeResolver codeContentResolver)
        {
            this.codeContentResolver = codeContentResolver;
        }

        public async UniTask<Result<DownloadedOrCachedData>> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct)
        {
            Result<DownloadedCodeContent> result = await codeContentResolver.GetCodeContent(path, ct);

            if (result.Success)
                return Result<DownloadedOrCachedData>.SuccessResult(
                    new DownloadedOrCachedData(result.Value));
            else
                return Result<DownloadedOrCachedData>.ErrorResult(result.ErrorMessage ?? "null");
        }
    }
}
