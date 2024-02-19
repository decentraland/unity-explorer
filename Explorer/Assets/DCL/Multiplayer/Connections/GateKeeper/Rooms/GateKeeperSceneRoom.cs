using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.PlacesAPIService;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
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
        private readonly string sceneHandleUrl;
        private CancellationTokenSource? cancellationTokenSource;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            ICharacterObject characterObject,
            IPlacesAPIService placesAPIService,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-handler"
        )
        {
            this.webRequests = webRequests;
            this.characterObject = characterObject;
            this.placesAPIService = placesAPIService;
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

        private CancellationToken CancellationToken()
        {
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTaskVoid RunAsync()
        {
            var token = CancellationToken();
            var meta = await MetaDataAsync();
            Debug.Log($"Send request with meta {meta.ToJson()}");
            var result = await webRequests.SignedFetch(sceneHandleUrl, meta.ToJson(), token);
            Debug.Log($"Result {result.UnityWebRequest.result}");
        }

        private async UniTask<MetaData> MetaDataAsync()
        {
            (string parcelId, string realmName) = await UniTask.WhenAll(ParcelIdAsync(), RealmNameAsync());
            return new MetaData(realmName, parcelId);
        }

        private async UniTask<string> ParcelIdAsync()
        {
            var position = characterObject.Position;
            var parcel = ParcelMathHelper.WorldToGridPosition(position);
            var result = await placesAPIService.GetPlaceAsync(parcel, CancellationToken());
            return result.EnsureNotNull($"parcel not found on coordinates {parcel}").id;
        }

        //TODO include name and sceneId
        private UniTask<string> RealmNameAsync() =>
            UniTask.FromResult("TODO"); //TODO

        [Serializable]
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
    }
}
