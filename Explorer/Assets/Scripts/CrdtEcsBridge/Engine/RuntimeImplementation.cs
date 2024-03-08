using Arch.Core;
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
using SceneRunner.Scene;
using SceneRunner.Scene.ExceptionsHandling;
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
        private readonly IJsOperations jsOperations;
        private readonly ISceneData sceneData;
        private readonly IWorldTimeProvider timeProvider;
        private readonly ISceneExceptionsHandler exceptionsHandler;
        private readonly World world;

        public RuntimeImplementation(IJsOperations jsOperations, ISceneData sceneData, IWorldTimeProvider timeProvider, ISceneExceptionsHandler exceptionsHandler, World world)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
            this.timeProvider = timeProvider;
            this.exceptionsHandler = exceptionsHandler;
            this.world = world;
        }

        public void Dispose()
        { }

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

        private static readonly QueryDescription GET_REALM_COMPONENT = new QueryDescription().WithAll<RealmComponent>();
        private const bool IS_PREVIEW_DEFAULT_VALUE = false;

        public async UniTask<IRuntime.GetRealmResponse> GetRealmAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();

            IRuntime.RealmInfo realmInfo = new IRuntime.RealmInfo();

            world.Query(in GET_REALM_COMPONENT, (ref RealmComponent b) =>
            {
                realmInfo.RealmName = b.RealmData.RealmName;
                realmInfo.NetworkID = b.RealmData.NetworkId;
                realmInfo.IsPreview = IS_PREVIEW_DEFAULT_VALUE;
                realmInfo.CommsAdapter = b.RealmData.CommsAdapter;
                realmInfo.BaseURL = b.Ipfs.CatalystBaseUrl.Value;
            });

            return new IRuntime.GetRealmResponse
            {
                RealmInfo = realmInfo
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
    }
}
