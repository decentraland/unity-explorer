using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoomProvider : IGateKeeperSceneRoomProvider
    {
        private readonly IWebRequestController webRequests;
        private readonly IMetaDataSource metaDataSource;
        private readonly ConnectiveRoom connectiveRoom;
        private readonly string sceneHandleUrl;
        private MetaData? previousMetaData;

        private readonly InteriorRoom interiorRoom;

        public GateKeeperSceneRoomProvider(
            IWebRequestController webRequests,
            IMetaDataSource metaDataSource,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-adapter"
        )
        {
            this.webRequests = webRequests;
            this.metaDataSource = metaDataSource;
            this.sceneHandleUrl = sceneHandleUrl;

            connectiveRoom = new ConnectiveRoom(
                interiorRoom = new InteriorRoom(), // not shared room
                static _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync
            );
        }

        public IRoom Room() =>
            interiorRoom;

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public UniTask StartAsync(CancellationToken ct)
        {
            connectiveRoom.Start();
            return UniTask.CompletedTask;
        }

        public UniTask StopAsync(CancellationToken ct) =>
            connectiveRoom.StopAsync(ct);

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, CancellationToken token)
        {
            MetaData meta = await metaDataSource.MetaDataAsync(token);

            if (connectiveRoom.CurrentState() is not IConnectiveRoom.State.Running || meta.Equals(previousMetaData) == false)
            {
                string connectionString = await ConnectionStringAsync(meta, token);
                await connectToRoomAsyncDelegate(connectionString, token);
            }

            previousMetaData = meta;
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            GenericPostRequest result = await webRequests.SignedFetchPostAsync(sceneHandleUrl, meta.ToJson(), token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
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
