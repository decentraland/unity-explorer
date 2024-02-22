using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IMetaDataSource metaDataSource;
        private readonly IWebRequestController webRequests;
        private readonly IMultiPool multiPool;
        private readonly string sceneHandleUrl;
        private readonly InteriorRoom room = new ();
        private readonly TimeSpan heartbeatsInterval = TimeSpan.FromSeconds(1);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Sleep);

        private CancellationTokenSource? cancellationTokenSource;
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
            this.multiPool = multiPool;
            this.sceneHandleUrl = sceneHandleUrl;
        }

        public void Start() =>
            RunAsync().Forget();

        public void Stop()
        {
            roomState.Set(IConnectiveRoom.State.Sleep);
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        public IConnectiveRoom.State CurrentState() =>
            roomState.Value();

        public IRoom Room() =>
            room;

        private CancellationToken StopPreviousAndNewCancellationToken()
        {
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTaskVoid RunAsync()
        {
            CancellationToken token = StopPreviousAndNewCancellationToken();
            roomState.Set(IConnectiveRoom.State.Starting);

            while (token.IsCancellationRequested == false)
            {
                await TryToConnectToNewRoom(token);
                await UniTask.Delay(heartbeatsInterval, cancellationToken: token);
            }
        }

        private async UniTask TryToConnectToNewRoom(CancellationToken token)
        {
            MetaData meta = await metaDataSource.MetaDataAsync(token);

            if (meta.Equals(previousMetaData) == false)
            {
                string connectionString = await ConnectionStringAsync(meta, token);
                Debug.Log($"String is: {connectionString}");
                await ConnectToRoomAsync(connectionString, token);
            }

            previousMetaData = meta;
        }

        private async UniTask ConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            var newRoom = multiPool.Get<LogRoom>();
            await newRoom.EnsuredConnectAsync(connectionString, multiPool, token);
            room.Assign(newRoom, out IRoom? previous);
            previous?.Disconnect();
            multiPool.TryRelease(previous);
            roomState.Set(IConnectiveRoom.State.Running);
            Debug.Log("Successful connection");
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            GenericPostRequest result = await webRequests.SignedFetchAsync(sceneHandleUrl, meta.ToJson(), token);
            var response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            return response.adapter;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }
    }
}
