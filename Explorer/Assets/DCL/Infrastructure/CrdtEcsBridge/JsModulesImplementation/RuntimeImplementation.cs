using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SkyBox;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Microsoft.ClearScript.JavaScript;
using SceneRunner.Scene;
using SceneRuntime;
using SceneRuntime.Apis.Modules.Runtime;
using System;
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
        private readonly IRealmData realmData;
        private readonly IWebRequestController webRequestController;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly IRoomHub roomHub;

        public RuntimeImplementation(IJsOperations jsOperations, ISceneData sceneData, IRealmData realmData, IWebRequestController webRequestController, SkyboxSettingsAsset skyboxSettings, IRoomHub roomHub)
        {
            this.jsOperations = jsOperations;
            this.sceneData = sceneData;
            this.realmData = realmData;
            this.webRequestController = webRequestController;
            this.skyboxSettings = skyboxSettings;
            this.roomHub = roomHub;
        }

        public void Dispose() { }

        public async UniTask<IRuntime.ReadFileResponse> ReadFileAsync(string fileName, CancellationToken ct)
        {
            sceneData.TryGetContentUrl(fileName, out URLAddress url);
            sceneData.TryGetHash(fileName, out string hash);

            await UniTask.SwitchToMainThread();

            async UniTask<StreamableLoadingResult<ITypedArray<byte>>> CreateFileRequestAsync(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
            {
                using DownloadHandler? downloadHandler = await webRequestController.GetAsync(intention.CommonArguments.URL, ct, ReportCategory.JAVASCRIPT).ExposeDownloadHandlerAsync();
                NativeArray<byte>.ReadOnly nativeBytes = downloadHandler.nativeData;

                await UniTask.SwitchToThreadPool();

                // create script byte array
                ITypedArray<byte> array = jsOperations.NewUint8Array(nativeBytes.Length);

                // transfer data to script byte array
                array.Write(nativeBytes, (ulong)nativeBytes.Length, 0);
                return new StreamableLoadingResult<ITypedArray<byte>>(array);
            }

            var intent = new SubIntention(new CommonLoadingArguments(url));
            ITypedArray<byte> content = (await intent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, CreateFileRequestAsync, ReportCategory.JAVASCRIPT, ct)).UnwrapAndRethrow();

            return new IRuntime.ReadFileResponse
            {
                content = content,
                hash = hash,
            };
        }

        public UniTask<IRuntime.GetRealmResponse> GetRealmAsync(CancellationToken ct)
        {
            // Fetch dynamic room info from IRoomHub
            string room = string.Empty;
            bool isConnectedSceneRoom = false;

            try
            {
                IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();
                isConnectedSceneRoom = sceneRoom.CurrentState() == IConnectiveRoom.State.Running && sceneRoom.IsSceneConnected(sceneData.SceneEntityDefinition.id);
                room = roomHub.IslandRoom().Info.Sid ?? string.Empty;
            }
            catch (Exception)
            {
                // If roomHub is not available, use empty values
            }

            var realmInfo = new IRuntime.RealmInfo(
                new Uri(realmData.Ipfs.CatalystBaseUrl.Value).GetLeftPart(UriPartial.Authority),
                realmData.RealmName,
                realmData.NetworkId,
                realmData.CommsAdapter,
                realmData.IsLocalSceneDevelopment,
                room,
                isConnectedSceneRoom
            );

            return UniTask.FromResult(new IRuntime.GetRealmResponse(realmInfo));
        }

        public async UniTask<IRuntime.GetWorldTimeResponse> GetWorldTimeAsync()
        {
            await using var _ = await ExecuteOnMainThreadScope.NewScopeWithReturnOnThreadPoolAsync();
            uint timeInSeconds = skyboxSettings.TimeOfDayInSeconds;

            return new IRuntime.GetWorldTimeResponse
            {
                seconds = timeInSeconds,
            };
        }

        public IRuntime.CurrentSceneEntityResponse GetSceneInformation() =>
            new (
                baseUrl: sceneData.SceneContent.ContentBaseUrl.Value,
                urn: sceneData.SceneEntityDefinition.id,
                content: sceneData.SceneEntityDefinition.content,
                metadataJson: sceneData.SceneEntityDefinition.metadata.OriginalJson
            );
    }
}
