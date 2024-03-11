using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Time;
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
        private const float DEFAULT_GET_WORLD_TIME = 0.01f;

        private readonly IJsOperations jsOperations;
        private readonly ISceneData sceneData;
        private readonly IWorldTimeProvider timeProvider;
        private readonly ISceneExceptionsHandler exceptionsHandler;

        public RuntimeImplementation(IJsOperations jsOperations, ISceneData sceneData, IWorldTimeProvider timeProvider, ISceneExceptionsHandler exceptionsHandler)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
            this.timeProvider = timeProvider;
            this.exceptionsHandler = exceptionsHandler;
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
