using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : ConnectiveRoom
    {
        private class Activatable : ActivatableConnectiveRoom, IGateKeeperSceneRoom
        {
            private readonly GateKeeperSceneRoom origin;

            public Activatable(GateKeeperSceneRoom origin, bool initialState = true) : base(origin, initialState)
            {
                this.origin = origin;
            }

            public bool IsSceneConnected(string? sceneId) =>
                origin.IsSceneConnected(sceneId);

            public ISceneData? ConnectedScene => origin.ConnectedScene;
        }

        private readonly IWebRequestController webRequests;
        private readonly ISceneRoomMetaDataSource metaDataSource;

        private readonly string sceneHandleUrl;
        private readonly IScenesCache scenesCache;

        /// <summary>
        ///     The scene the current LiveKit room corresponds to
        /// </summary>
        private ISceneFacade? connectedScene;

        private MetaData.Input previousMetaData;

        public ISceneData? ConnectedScene => connectedScene?.SceneData;

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
        }

        public IGateKeeperSceneRoom AsActivatable() =>
            new Activatable(this);

        public bool IsSceneConnected(string? sceneId) =>
            !metaDataSource.ScenesCommunicationIsIsolated || sceneId == connectedScene?.SceneData.SceneEntityDefinition.id;

        public override async UniTask StopAsync()
        {
            await base.StopAsync();

            // We need to reset the metadata, so we can later re-connect to the scene on RunConnectCycleStepAsync.ProcessMetaDataAsync
            // Otherwise flows like the logout->login will not work due to metadata not changing
            previousMetaData = default(MetaData.Input);
            connectedScene = null;
        }

        protected override RoomSelection SelectValidRoom() =>
            metaDataSource.GetMetadataInput().Equals(previousMetaData) ? RoomSelection.PREVIOUS : RoomSelection.NEW;

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
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
                    await DisconnectCurrentRoomAsync(token);

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
                        RoomSelection roomSelection = await TryConnectToRoomAsync(
                            connectionString,
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
                        while (CurrentState() is IConnectiveRoom.State.Running
                               && !metaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }

                await waitForReconnectionRequiredTask;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // if we don't catch an exception, any failure leads to the loop being stopped
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Exception occured in {nameof(CycleStepAsync)} when {meta} was being processed: {e}");
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
