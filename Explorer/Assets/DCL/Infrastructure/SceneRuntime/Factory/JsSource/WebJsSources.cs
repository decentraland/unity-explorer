using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using System.Threading;
using Unity.Collections;
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

        public async UniTask<Result<SlicedOwnedMemory<byte>>>
            SceneSourceCodeAsync(URLAddress path, CancellationToken ct)
        {
            Result<DownloadedCodeContent> result
                = await codeContentResolver.GetCodeContent(path, ct);

            if (result.Success)
            {
                using DownloadedCodeContent content = result.Value;
                NativeArray<byte>.ReadOnly data = content.nativeData;

                await UniTask.SwitchToThreadPool();

                var sourceCode = new SlicedOwnedMemory<byte>(data.Length);
                data.AsReadOnlySpan().CopyTo(sourceCode.Memory.Span);

                await UniTask.SwitchToMainThread();

                return Result<SlicedOwnedMemory<byte>>.SuccessResult(
                    sourceCode);
            }
            else
                return Result<SlicedOwnedMemory<byte>>.ErrorResult(
                    result.ErrorMessage ?? "null");
        }
    }
}
