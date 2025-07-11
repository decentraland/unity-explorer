using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.GateKeeper.Rooms.Options;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class StopwatchSampler
    {
        private readonly string measurementName;
        private readonly List<long> samples = new ();
        private const int MAX_SAMPLES = 100;

        public StopwatchSampler(string measurementName)
        {
            this.measurementName = measurementName;
        }

        public void AddSample(long milliseconds)
        {
            samples.Add(milliseconds);

            if (samples.Count >= MAX_SAMPLES)
            {
                double average = samples.Average();
                Debug.Log($"{measurementName}: {average:F2} ms (based on {samples.Count} samples)");
                samples.Clear();
            }
        }
    }

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
        private readonly IScenesCache scenesCache;
        private readonly GateKeeperSceneRoomOptions options;

        // Stopwatch samplers for collecting 100 samples and calculating averages
        private static readonly StopwatchSampler connectionStringSampler = new ("CONNECTION STRING");
        private static readonly StopwatchSampler roomConnectionSampler = new ("ROOM CONNECTION");
        private static readonly StopwatchSampler globalActionSampler = new ("GLOBAL ACTION");

        /// <summary>
        ///     The scene the current LiveKit room corresponds to
        /// </summary>
        private ISceneFacade? connectedScene;

        private MetaData previousMetaData;

        public ISceneData? ConnectedScene => connectedScene?.SceneData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            IScenesCache scenesCache,
            GateKeeperSceneRoomOptions options
        )
        {
            this.webRequests = webRequests;
            this.scenesCache = scenesCache;
            this.options = options;
        }

        public IGateKeeperSceneRoom AsActivatable() =>
            new Activatable(this);

        private bool IsSceneConnected(string? sceneId) =>
            !options.SceneRoomMetaDataSource.ScenesCommunicationIsIsolated || sceneId == connectedScene?.SceneData.SceneEntityDefinition.id;

        public override async UniTask StopAsync()
        {
            await base.StopAsync();

            // We need to reset the metadata, so we can later re-connect to the scene on RunConnectCycleStepAsync.ProcessMetaDataAsync
            // Otherwise flows like the logout->login will not work due to metadata not changing
            previousMetaData = default(MetaData);
            connectedScene = null;
        }

        protected override RoomSelection SelectValidRoom() =>
            options.SceneRoomMetaDataSource.GetMetadataInput().Equals(previousMetaData) ? RoomSelection.PREVIOUS : RoomSelection.NEW;

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
                    connectedScene = null;
                    await DisconnectCurrentRoomAsync(true, token);

                    // After disconnection we need to wait for metadata to change
                    waitForReconnectionRequiredTask = WaitForMetadataIsDirtyAsync(token);

                    previousMetaData = meta;

                    async UniTask WaitForMetadataIsDirtyAsync(CancellationToken token)
                    {
                        while (!options.SceneRoomMetaDataSource.MetadataIsDirty)
                            await UniTask.Yield(token);
                    }
                }
                else
                {
                    if (!meta.Equals(previousMetaData))
                    {
                        Debug.Log("JUANI STARTING CONNECTION TO NEW ROOM");
                        var globalStopwatch = Stopwatch.StartNew();
                        var stopwatch = Stopwatch.StartNew();
                        string connectionString = await ConnectionStringAsync(meta, token);
                        stopwatch.Stop();
                        Debug.Log($"JUANI GETTING CONNECTION STRING TOOK {stopwatch.ElapsedMilliseconds} ms");
                        connectionStringSampler.AddSample(stopwatch.ElapsedMilliseconds);

                        // if the player returns to the previous scene but the new room has been connected, the previous connection should be preserved
                        // and the new connection should be discarded

                        stopwatch.Restart();
                        RoomSelection roomSelection = await TryConnectToRoomAsync(
                            connectionString,
                            token);

                        stopwatch.Stop();
                        Debug.Log($"JUANI CONNECTING TO ROOM TOOK {meta.sceneId} TOOK {stopwatch.ElapsedMilliseconds} ms");
                        roomConnectionSampler.AddSample(stopwatch.ElapsedMilliseconds);

                        globalStopwatch.Stop();
                        Debug.Log($"JUANI GLOBAL ACTION TOOK {globalStopwatch.ElapsedMilliseconds} ms");
                        globalActionSampler.AddSample(globalStopwatch.ElapsedMilliseconds);

                        if (roomSelection == RoomSelection.NEW)
                        {
                            previousMetaData = meta;
                            scenesCache.TryGetByParcel(meta.Parcel, out connectedScene);
                        }
                    }

                    waitForReconnectionRequiredTask = WaitForReconnectionRequiredAsync(token);

                    // Either room has disconnected or metadata has changed
                    async UniTask WaitForReconnectionRequiredAsync(CancellationToken token)
                    {
                        while (CurrentState() is IConnectiveRoom.State.Running
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
            AdapterResponse response = await webRequests.SignedFetchPostAsync(
                                                             url,
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
