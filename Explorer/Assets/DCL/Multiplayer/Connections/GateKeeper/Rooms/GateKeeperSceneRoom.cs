using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using ECS.SceneLifeCycle;
using LiveKit.Rooms;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly ISceneRoomMetaDataSource metaDataSource;
        private readonly IConnectiveRoom connectiveRoom;
        private readonly string sceneHandleUrl;

        private readonly IScenesCache scenesCache;

        /// <summary>
        ///     The scene the current LiveKit room corresponds to
        /// </summary>
        private ISceneFacade? connectedScene;

        private MetaData.Input previousMetaData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            ISceneRoomMetaDataSource metaDataSource,
            IDecentralandUrlsSource decentralandUrlsSource,
            IScenesCache scenesCache)
        {
            this.webRequests = webRequests;
            this.metaDataSource = metaDataSource;
            this.scenesCache = scenesCache;
            sceneHandleUrl = decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter);

            connectiveRoom = new ConnectiveRoom(
                static _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync,
                nameof(GateKeeperSceneRoom)
            );
        }

        public ISceneData? ConnectedScene => connectedScene?.SceneData;

        public bool IsSceneConnected(string? sceneId) =>
            !metaDataSource.ScenesCommunicationIsIsolated || sceneId == connectedScene?.SceneData.SceneEntityDefinition.id;

        public UniTask<bool> StartAsync() =>
            connectiveRoom.StartAsync();

        public async UniTask StopAsync()
        {
            await connectiveRoom.StopAsync();
            // We need to reset the metadata, so we can later re-connect to the scene on RunConnectCycleStepAsync.ProcessMetaDataAsync
            // Otherwise flows like the logout->login will not work due to metadata not changing
            previousMetaData = default;
        }

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, DisconnectCurrentRoomAsyncDelegate disconnectCurrentRoomAsyncDelegate, CancellationToken token)
        {
            MetaData meta = default;

            try
            {
                MetaData.Input metaInput = metaDataSource.GetMetadataInput();
                meta = await metaDataSource.MetaDataAsync(metaInput, token);

                UniTask waitForReconnectionRequiredTask;

                // Disconnect if no sceneId assigned, disconnection can't be interrupted
                if (meta.sceneId == null)
                {
                    connectedScene = null;
                    await disconnectCurrentRoomAsyncDelegate(token);

                    // After disconnection we need to wait for metadata to change
                    waitForReconnectionRequiredTask = WaitForMetadataIsDirtyAsync(token);

                    previousMetaData = metaInput;

                    async UniTask WaitForMetadataIsDirtyAsync(CancellationToken token)
                    {
                        while (!metaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }
                else
                {
                    if (!metaInput.Equals(previousMetaData))
                    {
                        string connectionString = await ConnectionStringAsync(meta, token);

                        // if the player returns to the previous scene but the new room has been connected, the previous connection should be preserved
                        // and the new connection should be discarded
                        RoomSelection roomSelection = await connectToRoomAsyncDelegate(
                            connectionString,
                            () => metaDataSource.GetMetadataInput().Equals(previousMetaData) ? RoomSelection.PREVIOUS : RoomSelection.NEW,
                            token);

                        if (roomSelection == RoomSelection.NEW)
                        {
                            previousMetaData = metaInput;
                            scenesCache.TryGetByParcel(metaInput.Parcel, out connectedScene);
                        }
                    }

                    waitForReconnectionRequiredTask = WaitForReconnectionRequiredAsync(token);

                    // Either room has disconnected or metadata has changed
                    async UniTask WaitForReconnectionRequiredAsync(CancellationToken token)
                    {
                        while (connectiveRoom.CurrentState() is IConnectiveRoom.State.Running
                               && !metaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }

                await waitForReconnectionRequiredTask;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // if we don't catch an exception, any failure leads to the loop being stopped
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Exception occured in {nameof(RunConnectCycleStepAsync)} when {meta} was being processed: {e}");
            }
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            AdapterResponse response = await webRequests.SignedFetchPostAsync(
                                                             sceneHandleUrl,
                                                             meta.ToJson(),
                                                             token)
                                                        .CreateFromJson<AdapterResponse>(WRJsonParser.Unity);

            string connectionString = response.adapter;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"String is: {connectionString}");
            return connectionString;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }
    }
}
