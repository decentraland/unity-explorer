using Cysharp.Threading.Tasks;
using Diagnostics.ReportsHandling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules;
using System.Threading;
using Unity.Collections;
using UnityEngine.Networking;
using Utility;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    /// Unique instance for each Scene Runtime
    /// </summary>
    public class RuntimeImplementation : IRuntime
    {
        private readonly IJSOperations jsOperations;
        private readonly ISceneData sceneData;

        public RuntimeImplementation(IJSOperations jsOperations, ISceneData sceneData)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
        }

        public async UniTask<ITypedArray<byte>> ReadFile(string fileName, CancellationToken ct)
        {
            sceneData.TryGetContentUrl(fileName, out string url);

            await UniTask.SwitchToMainThread();

            async UniTask<StreamableLoadingResult<ITypedArray<byte>>> CreateFileRequest(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
            {
                using UnityWebRequest wr = await UnityWebRequest.Get(intention.CommonArguments.URL).SendWebRequest().WithCancellation(ct);
                NativeArray<byte>.ReadOnly nativeBytes = wr.downloadHandler.nativeData;

                await UniTask.SwitchToThreadPool();

                // create script byte array
                ITypedArray<byte> array = jsOperations.CreateUint8Array(nativeBytes.Length);

                // transfer data to script byte array
                array.Write(nativeBytes, (ulong)nativeBytes.Length, 0);
                return new StreamableLoadingResult<ITypedArray<byte>>(array);
            }

            var intent = new SubIntention(new CommonLoadingArguments(url));
            return (await intent.RepeatLoop(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, CreateFileRequest, ReportCategory.ENGINE, ct)).UnwrapAndRethrow();
        }

        public void Dispose() { }
    }
}
