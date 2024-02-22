using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly ICharacterObject characterObject;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMultiPool multiPool;
        private readonly IRealmData realmData;
        private readonly string sceneHandleUrl;
        private readonly InteriorRoom room = new ();
        private readonly TimeSpan heartbeatsInterval = TimeSpan.FromSeconds(1);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Sleep);

        private CancellationTokenSource? cancellationTokenSource;
        private MetaData? previousMetaData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            ICharacterObject characterObject,
            IPlacesAPIService placesAPIService,
            IMultiPool multiPool,
            IRealmData realmData,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-adapter"
        )
        {
            this.webRequests = webRequests;
            this.characterObject = characterObject;
            this.placesAPIService = placesAPIService;
            this.multiPool = multiPool;
            this.realmData = realmData;
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
            roomState.Value();//TODO change state

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
            MetaData meta = await MetaDataAsync(token);

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
            Room newRoom = multiPool.Get<Room>();
            await newRoom.EnsuredConnectAsync(connectionString, multiPool, token);
            room.Assign(newRoom, out IRoom? previous);
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

        private async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            string parcelId = await ParcelIdAsync(token);
            return new MetaData(realmData.RealmName, parcelId);
        }

        private async UniTask<string> ParcelIdAsync(CancellationToken token)
        {
            Vector3 position = characterObject.Position;
            Vector2Int parcel = ParcelMathHelper.WorldToGridPosition(position);
            PlacesData.PlaceInfo result = await placesAPIService.GetPlaceAsync(parcel, token);
            return result.EnsureNotNull($"parcel not found on coordinates {parcel}").id;
        }

        [Serializable]
        [SuppressMessage("ReSharper", "NotAccessedField.Local")]
        private struct MetaData
        {
            public string realmName;
            public string sceneId;

            public MetaData(string realmName, string sceneId)
            {
                this.realmName = realmName;
                this.sceneId = sceneId;
            }

            public string ToJson() =>
                JsonUtility.ToJson(this)!;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }
    }
}
