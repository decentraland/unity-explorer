using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Proto;
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
                origin.CurrentSceneRoomConnected += OnCurrentSceneRoomConnected;
                origin.CurrentSceneRoomDisconnected += OnCurrentSceneRoomDisconnected;
                origin.CurrentSceneRoomForbiddenAccess += OnCurrentSceneRoomForbiddenAccess;
            }

            public bool IsSceneConnected(string? sceneId) =>
                origin.IsSceneConnected(sceneId);

            public override void Dispose()
            {
                origin.CurrentSceneRoomConnected -= OnCurrentSceneRoomConnected;
                origin.CurrentSceneRoomDisconnected -= OnCurrentSceneRoomDisconnected;
                origin.CurrentSceneRoomForbiddenAccess -= OnCurrentSceneRoomForbiddenAccess;
                base.Dispose();
            }

            public event Action? CurrentSceneRoomConnected;
            public event Action? CurrentSceneRoomDisconnected;
            public event Action? CurrentSceneRoomForbiddenAccess;
            public MetaData? ConnectedScene => origin.ConnectedScene;

            private void OnCurrentSceneRoomConnected() =>
                CurrentSceneRoomConnected?.Invoke();

            private void OnCurrentSceneRoomDisconnected() =>
                CurrentSceneRoomDisconnected?.Invoke();

            private void OnCurrentSceneRoomForbiddenAccess() =>
                CurrentSceneRoomForbiddenAccess?.Invoke();
        }

        private readonly IWebRequestController webRequests;
        private readonly GateKeeperSceneRoomOptions options;

        private event Action? CurrentSceneRoomConnected;
        private event Action? CurrentSceneRoomDisconnected;
        private event Action? CurrentSceneRoomForbiddenAccess;
        private MetaData? currentMetaData;

        public MetaData? ConnectedScene => currentMetaData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            GateKeeperSceneRoomOptions options
        )
        {
            this.webRequests = webRequests;
            this.options = options;
        }

        public IGateKeeperSceneRoom AsActivatable() =>
            new Activatable(this);

        private bool IsSceneConnected(string? sceneId) =>
            !options.SceneRoomMetaDataSource.ScenesCommunicationIsIsolated || string.Equals(sceneId, currentMetaData?.sceneId, StringComparison.OrdinalIgnoreCase);

        public override async UniTask StopAsync()
        {
            await base.StopAsync();

            // We need to reset the metadata, so we can later re-connect to the scene on RunConnectCycleStepAsync.ProcessMetaDataAsync
            // Otherwise flows like the logout->login will not work due to metadata not changing
            currentMetaData = null;

            CurrentSceneRoomDisconnected?.Invoke();
        }

        protected override void OnForbiddenAccess()
        {
            base.OnForbiddenAccess();

            // We need to notify the upper layer that the current room is forbidden (that means the player is banned)
            CurrentSceneRoomForbiddenAccess?.Invoke();
        }

        protected override RoomSelection SelectValidRoom() =>
            options.SceneRoomMetaDataSource.GetMetadataInput().Equals(currentMetaData.GetValueOrDefault()) ? RoomSelection.PREVIOUS : RoomSelection.NEW;

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            MetaData meta = default;

            try
            {
                var result = await options.SceneRoomMetaDataSource.MetaDataAsync(options.SceneRoomMetaDataSource.GetMetadataInput(), token);

                if (result.Success == false)
                    return;

                meta = result.Value;

                UniTask waitForReconnectionRequiredTask;

                // Disconnect if no sceneId assigned, disconnection can't be interrupted
                if (meta.sceneId == null)
                {
                    await DisconnectCurrentRoomAsync(true, token);
                    CurrentSceneRoomDisconnected?.Invoke();

                    // After disconnection we need to wait for metadata to change
                    waitForReconnectionRequiredTask = WaitForMetadataIsDirtyAsync(token);

                    currentMetaData = meta;

                    async UniTask WaitForMetadataIsDirtyAsync(CancellationToken token)
                    {
                        while (!options.SceneRoomMetaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }
                else
                {
                    if (!meta.Equals(currentMetaData.GetValueOrDefault()) || Room().Info.ConnectionState == ConnectionState.ConnDisconnected)
                    {
                        string connectionString = await ConnectionStringAsync(meta, token);

                        if (Room().Info.ConnectionState == ConnectionState.ConnDisconnected)
                            currentMetaData = null;

                        // if the player returns to the previous scene but the new room has been connected, the previous connection should be preserved
                        // and the new connection should be discarded
                        RoomSelection roomSelection = await TryConnectToRoomAsync(
                            connectionString,
                            token);

                        CurrentSceneRoomConnected?.Invoke();

                        if (roomSelection == RoomSelection.NEW)
                        {
                            currentMetaData = meta;
                        }
                    }

                    waitForReconnectionRequiredTask = WaitForReconnectionRequiredAsync(token);

                    // Either room has disconnected or metadata has changed
                    async UniTask WaitForReconnectionRequiredAsync(CancellationToken token)
                    {
                        while (CurrentState() is IConnectiveRoom.State.Running
                               && Room().Info.ConnectionState == ConnectionState.ConnConnected
                               && !options.SceneRoomMetaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }

                await waitForReconnectionRequiredTask;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // if we don't catch an exception, any failure leads to the loop being stopped
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Exception occured in {nameof(CycleStepAsync)} when {meta} was being processed: {e}");

                // The upper layer has a recovery loop on its own so notify it
                throw;
            }
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            string url = options.AdapterUrl;
            string json = meta.ToJson();

            AdapterResponse response = await webRequests
                                            .SignedFetchPostAsync(url, json, token)
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
