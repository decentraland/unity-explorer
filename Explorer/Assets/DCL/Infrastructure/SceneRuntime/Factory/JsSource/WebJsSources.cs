using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision.CodeResolver;
using DCL.Diagnostics;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using System;
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

                SlicedOwnedMemory<byte> sourceCode;

                if (data.AsReadOnlySpan()
                        .Slice(0, SceneRuntimeFactory.COMMONJS_HEADER_UTF8.Length)
                        .SequenceEqual(SceneRuntimeFactory.COMMONJS_HEADER_UTF8))
                {
                    sourceCode = new SlicedOwnedMemory<byte>(data.Length);
                    data.AsReadOnlySpan().CopyTo(sourceCode.Memory.Span);
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.SCENE_FACTORY,
                        $"The code of the scene at \"{path.Value}\" does not include the CommonJS module wrapper. This is suboptimal.");

                    sourceCode = new SlicedOwnedMemory<byte>(
                        SceneRuntimeFactory.COMMONJS_HEADER_UTF8.Length + data.Length +
                        SceneRuntimeFactory.COMMONJS_FOOTER_UTF8.Length);

                    SceneRuntimeFactory.COMMONJS_HEADER_UTF8.CopyTo(sourceCode.Memory);

                    data.AsReadOnlySpan().CopyTo(sourceCode.Memory.Slice(
                        SceneRuntimeFactory.COMMONJS_HEADER_UTF8.Length).Span);

                    SceneRuntimeFactory.COMMONJS_FOOTER_UTF8.CopyTo(sourceCode.Memory.Slice(
                        SceneRuntimeFactory.COMMONJS_HEADER_UTF8.Length + data.Length));
                }

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
