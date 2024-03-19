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
using SceneRuntime.Apis.Modules;
using System;
using System.Threading;
using Unity.Collections;
using UnityEngine.Networking;
using Utility;

namespace CrdtEcsBridge.Engine
{
    /// <summary>
    ///     Unique instance for each Scene Runtime
    /// </summary>
    public class RuntimeImplementation : IRuntime
    {
        private const bool IS_PREVIEW_DEFAULT_VALUE = false;

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

            var realmInfo = new IRuntime.RealmInfo();

            if (realmData != null)
            {
                realmInfo.realmName = realmData.RealmName;
                realmInfo.networkId = realmData.NetworkId;
                realmInfo.isPreview = IS_PREVIEW_DEFAULT_VALUE;
                realmInfo.commsAdapter = realmData.CommsAdapter;
                realmInfo.baseURL = realmData.Ipfs.CatalystBaseUrl.Value;
            }

            return new IRuntime.GetRealmResponse
            {
                realmInfo = realmInfo,
            };
        }

        public async UniTask<IRuntime.GetWorldTimeResponse> GetWorldTimeAsync(CancellationToken ct)
        {
            float seconds = await timeProvider.GetWorldTimeAsync(ct);

            return new IRuntime.GetWorldTimeResponse
            {
                seconds = seconds,
            };
        }

        public IRuntime.CurrentSceneEntityResponse GetSceneInformation() =>
            new ()
            {
                baseUrl = sceneData.SceneContent.ContentBaseUrl.Value,
                contentMapping = sceneData.SceneEntityDefinition.content,
                urn = sceneData.SceneEntityDefinition.id,
                metadataJson = JsonConvert.SerializeObject(sceneData.SceneEntityDefinition.metadata),
            };
    }
}
