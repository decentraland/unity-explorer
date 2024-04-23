using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Time;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.Runtime;
using System.Threading;
using Unity.Collections;
using UnityEngine.Networking;
using Utility;
using Utility.Multithreading;

namespace CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    ///     Unique instance for each Scene Runtime
    /// </summary>
    public class RuntimeImplementation : IRuntime
    {
        private readonly IJsOperations jsOperations;
        private readonly ISceneData sceneData;
        private readonly IWorldTimeProvider timeProvider;
        private readonly IRealmData realmData;

        public RuntimeImplementation(IJsOperations jsOperations, ISceneData sceneData, IWorldTimeProvider timeProvider, IRealmData realmData)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
            this.timeProvider = timeProvider;
            this.realmData = realmData;
        }

        public void Dispose() { }

        public async UniTask<IRuntime.ReadFileResponse> ReadFileAsync(string fileName, CancellationToken ct)
        {
            sceneData.TryGetContentUrl(fileName, out URLAddress url);
            sceneData.TryGetHash(fileName, out string hash);

            await UniTask.SwitchToMainThread();

            async UniTask<StreamableLoadingResult<ITypedArray<byte>>> CreateFileRequestAsync(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
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
            ITypedArray<byte> content = (await intent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, CreateFileRequestAsync, ReportCategory.ENGINE, ct)).UnwrapAndRethrow();

            return new IRuntime.ReadFileResponse
            {
                content = content,
                hash = hash,
            };
        }

        public async UniTask<IRuntime.GetRealmResponse> GetRealmAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            return new IRuntime.GetRealmResponse(realmData);
        }

        public async UniTask<IRuntime.GetWorldTimeResponse> GetWorldTimeAsync(CancellationToken ct)
        {
            await using var _ = await ExecuteOnMainThreadScope.NewScopeWithReturnOnThreadPoolAsync();
            float seconds = await timeProvider.GetWorldTimeAsync(ct);

            return new IRuntime.GetWorldTimeResponse
            {
                seconds = seconds,
            };
        }

        public IRuntime.CurrentSceneEntityResponse GetSceneInformation() =>
            new (
                baseUrl: sceneData.SceneContent.ContentBaseUrl.Value,
                urn: sceneData.SceneEntityDefinition.id,
                content: sceneData.SceneEntityDefinition.content,
                metadataJson: JsonConvert.SerializeObject(sceneData.SceneEntityDefinition.metadata)
            );
    }
}
