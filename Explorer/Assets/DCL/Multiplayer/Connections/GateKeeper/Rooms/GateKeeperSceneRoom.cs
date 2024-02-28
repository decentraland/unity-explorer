using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;

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
            IMultiPool multiPool,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-adapter"
        )
        {
            this.webRequests = webRequests;
            this.metaDataSource = metaDataSource;
            this.sceneHandleUrl = sceneHandleUrl;

            connectiveRoom = new ConnectiveRoom(
                _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync,
                multiPool
            );
        }

        public void Start() =>
            connectiveRoom.Start();

        public void Stop() =>
            connectiveRoom.Stop();

        public IConnectiveRoom.State CurrentState() =>
            connectiveRoom.CurrentState();

        public IRoom Room() =>
            connectiveRoom.Room();

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, CancellationToken token)
        {
            MetaData meta = await metaDataSource.MetaDataAsync(token);

            if (meta.Equals(previousMetaData) == false)
            {
                string connectionString = await ConnectionStringAsync(meta, token);
                ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log($"String is: {connectionString}");
                await connectToRoomAsyncDelegate(connectionString, token);
            }

            previousMetaData = meta;
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            GenericPostRequest result = await webRequests.SignedFetchAsync(sceneHandleUrl, meta.ToJson(), token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            return response.adapter;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }
    }
}
