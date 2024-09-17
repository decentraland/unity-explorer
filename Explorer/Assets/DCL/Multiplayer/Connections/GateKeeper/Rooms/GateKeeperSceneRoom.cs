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
        private readonly Func<bool> roomIsNotRunning;

        private readonly IScenesCache scenesCache;

        /// <summary>
        ///     The scene the current LiveKit room corresponds to
        /// </summary>
        private ISceneFacade? connectedScene;

        private MetaData previousMetaData;

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

            roomIsNotRunning = () => connectiveRoom.CurrentState() is not IConnectiveRoom.State.Running;
        }

        public ISceneData? ConnectedScene => connectedScene?.SceneData;

        public UniTask<bool> StartAsync() =>
            connectiveRoom.StartAsync();

        public UniTask StopAsync() =>
            connectiveRoom.StopAsync();

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, DisconnectCurrentRoomAsyncDelegate disconnectCurrentRoomAsyncDelegate, CancellationToken token)
        {
            MetaData meta = await metaDataSource.MetaDataAsync(token);

            // Connect or disconnect, at the same time check if metadata has potentially changed
            await UniTask.WhenAll(WaitForMetadataDirty(token), ProcessMetaDataAsync(token));

            async UniTask ProcessMetaDataAsync(CancellationToken token)
            {
                if (meta.sceneId == null)
                {
                    connectedScene = null;
                    await disconnectCurrentRoomAsyncDelegate(token);
                }
                else if (!meta.Equals(previousMetaData))
                {
                    string connectionString = await ConnectionStringAsync(meta, token);
                    await connectToRoomAsyncDelegate(connectionString, token);
                    scenesCache.TryGetByParcel(meta.Parcel, out connectedScene);
                }

                previousMetaData = meta;
            }
        }

        /// <summary>
        ///     Either room has disconnected or metadata has changed
        /// </summary>
        /// <param name="token"></param>
        private async UniTask WaitForMetadataDirty(CancellationToken token)
        {
            await UniTask.WhenAny(UniTask.WaitUntil(roomIsNotRunning, cancellationToken: token), metaDataSource.WaitForMetaDataIsDirtyAsync(token));
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            AdapterResponse response = await webRequests.SignedFetchPostAsync(
                                                             sceneHandleUrl,
                                                             meta.ToJson(),
                                                             token)
                                                        .CreateFromJson<AdapterResponse>(WRJsonParser.Unity);

            string connectionString = response.adapter;
            ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log($"String is: {connectionString}");
            return connectionString;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }
    }
}
