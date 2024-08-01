using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly IMetaDataSource metaDataSource;
        private readonly IConnectiveRoom connectiveRoom;
        private readonly string sceneHandleUrl;
        private MetaData? previousMetaData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            IMetaDataSource metaDataSource,
            IDecentralandUrlsSource decentralandUrlsSource
        )
        {
            this.webRequests = webRequests;
            this.metaDataSource = metaDataSource;
            this.sceneHandleUrl = decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter);

            connectiveRoom = new ConnectiveRoom(
                static _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync,
                nameof(GateKeeperSceneRoom)
            );
        }

        public void Start() =>
            connectiveRoom.Start();

        public UniTask StopAsync() =>
            connectiveRoom.StopAsync();

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, DisconnectCurrentRoomAsyncDelegate disconnectCurrentRoomAsyncDelegate, CancellationToken token)
        {
            MetaData meta = await metaDataSource.MetaDataAsync(token);

            if (meta.sceneId == null)
            {
                await disconnectCurrentRoomAsyncDelegate(token);
                return;
            }

            if (connectiveRoom.CurrentState() is not IConnectiveRoom.State.Running || meta.Equals(previousMetaData) == false)
            {
                string connectionString = await ConnectionStringAsync(meta, token);
                await connectToRoomAsyncDelegate(connectionString, token);
            }

            previousMetaData = meta;
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
