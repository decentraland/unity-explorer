using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public class GateKeeperSceneRoom : IGateKeeperSceneRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly string sceneHandleUrl;
        private CancellationTokenSource? cancellationTokenSource;

        public GateKeeperSceneRoom(
            IWebRequestController webRequests,
            IWeb3IdentityCache web3IdentityCache,
            string sceneHandleUrl = "https://comms-gatekeeper.decentraland.zone/get-scene-handler"
        )
        {
            this.webRequests = webRequests;
            this.web3IdentityCache = web3IdentityCache;
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
            using var headers = new WebRequestHeadersInfo();

            //headers.Add(new WebRequestHeader("Authorization", "Bearer " + web3IdentityCache.GetAuthToken()));
            headers.Add(new WebRequestHeader("meta", "value"));
            var chain = web3IdentityCache.EnsuredIdentity().Sign(new MetaData("fake", "0").ToJson());

            var result = await webRequests.PostAsync(
                new CommonArguments(URLAddress.FromString(sceneHandleUrl)),
                GenericPostArguments.Empty,
                token,
                signInfo: new WebRequestSignInfo(URLAddress.FromString(sceneHandleUrl)),
                headersInfo: headers
            );

            Debug.Log($"Result {result.UnityWebRequest.result}");
        }

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
