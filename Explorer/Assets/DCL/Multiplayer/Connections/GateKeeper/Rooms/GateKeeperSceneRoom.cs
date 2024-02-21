using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly ICharacterObject characterObject;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMultiPool multiPool;
        private readonly string sceneHandleUrl;
        private readonly InteriorRoom room = new ();
        private readonly TimeSpan heartbeatsInterval = TimeSpan.FromSeconds(1);

        private CancellationTokenSource? cancellationTokenSource;
        private MetaData? previousMetaData;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            ICharacterObject characterObject,
            IPlacesAPIService placesAPIService,
            IMultiPool multiPool,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-handler"
        )
        {
            this.webRequests = webRequests;
            this.characterObject = characterObject;
            this.placesAPIService = placesAPIService;
            this.multiPool = multiPool;
            this.sceneHandleUrl = sceneHandleUrl;
        }

        public void Start() =>
            RunAsync().Forget();

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        public bool IsRunning() =>
            cancellationTokenSource is { IsCancellationRequested: false };

        public IRoom Room() =>
            throw new NotImplementedException();

        private CancellationToken StopPreviousAndNewCancellationToken()
        {
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTaskVoid RunAsync()
        {
            //TODO run in background
            CancellationToken token = StopPreviousAndNewCancellationToken();

            while (token.IsCancellationRequested == false)
            {
                //TODO but this on main thread
                MetaData meta = await MetaDataAsync(token);

                if (meta.Equals(previousMetaData) == false)
                {
                    string connectionString = await ConnectionStringAsync(meta, token);
                    Debug.Log($"String is: {connectionString}");
                    await ConnectToRoomAsync(connectionString, token);
                }

                previousMetaData = meta;
                await UniTask.Delay(heartbeatsInterval, cancellationToken: token);

                //TODO start checking position of player and reconnect on request
            }
        }

        private async UniTask ConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            Room newRoom = multiPool.Get<Room>();
            await newRoom.EnsuredConnectAsync(connectionString, multiPool, token);
            room.Assign(newRoom, out IRoom? previous);
            multiPool.TryRelease(previous);
        }

        private async UniTask<string> ConnectionStringAsync(MetaData meta, CancellationToken token)
        {
            GenericPostRequest result = await webRequests.SignedFetchAsync(sceneHandleUrl, meta.ToJson(), token);
            var response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            return response.adapter;
        }

        private async UniTask<MetaData> MetaDataAsync(CancellationToken token)
        {
            (string parcelId, string realmName) = await UniTask.WhenAll(ParcelIdAsync(token), RealmNameAsync());
            return new MetaData(realmName, parcelId);
        }

        private async UniTask<string> ParcelIdAsync(CancellationToken token)
        {
            //TODO to actual id fetching
            return "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq";
            Vector3 position = characterObject.Position;
            Vector2Int parcel = ParcelMathHelper.WorldToGridPosition(position);
            PlacesData.PlaceInfo result = await placesAPIService.GetPlaceAsync(parcel, token);
            return result.EnsureNotNull($"parcel not found on coordinates {parcel}").id;
        }

        //TODO include name and sceneId
        private UniTask<string> RealmNameAsync() =>
            UniTask.FromResult("TODO"); //TODO

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
