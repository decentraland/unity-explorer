using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed
{
    public class FixedConnectiveRoom : ConnectiveRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly ICurrentAdapterAddress currentAdapterAddress;

        public FixedConnectiveRoom(IWebRequestController webRequests, ICurrentAdapterAddress currentAdapterAddress)
        {
            this.webRequests = webRequests;
            this.currentAdapterAddress = currentAdapterAddress;
        }

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            var adapterUrl = new Uri(currentAdapterAddress.AdapterUrl());
            string metadata = FixedMetadata.Default.ToJson();
            GenericPostRequest? result = webRequests.SignedFetchPostAsync(adapterUrl, metadata, ReportCategory.LIVEKIT);
            AdapterResponse response = await result.CreateFromJsonAsync<AdapterResponse>(WRJsonParser.Unity, token);
            string connectionString = response.fixedAdapter;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"String is: {connectionString}");
            return connectionString;
        }

        [Serializable]
        private struct FixedMetadata
        {
            public static FixedMetadata Default = new ()
            {
                intent = "dcl:explorer:comms-handshake",
                signer = "dcl:explorer",
                isGuest = false,
            };

            public string intent;
            public string signer;
            public bool isGuest;

            public string ToJson() =>
                JsonUtility.ToJson(this)!;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string fixedAdapter;
        }
    }
}
