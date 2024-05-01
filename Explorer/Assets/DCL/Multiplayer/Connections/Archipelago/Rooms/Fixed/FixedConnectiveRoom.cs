using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed
{
    public class FixedConnectiveRoom : IConnectiveRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly IConnectiveRoom connectiveRoom;
        private readonly ICurrentAdapterAddress currentAdapterAddress;

        public FixedConnectiveRoom(IWebRequestController webRequests, ICurrentAdapterAddress currentAdapterAddress)
        {
            this.webRequests = webRequests;
            this.currentAdapterAddress = currentAdapterAddress;

            connectiveRoom = new ConnectiveRoom(
                static _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync
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

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, CancellationToken token)
        {
            if (connectiveRoom.CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await connectToRoomAsyncDelegate(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            string adapterUrl = await currentAdapterAddress.AdapterUrlAsync(token);
            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchPostAsync(adapterUrl, metadata, token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.fixedAdapter;
            ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log($"String is: {connectionString}");
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
