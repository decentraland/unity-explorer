using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Fixed
{
    /// <summary>
    /// No need for any abstractions - it's a unique behaviour that we can't replace
    /// </summary>
    internal class FixedConnectionRoomStrategy : IRealmRoomStrategy
    {
        private readonly IWebRequestController webRequests;
        private readonly string currentAdapterAddress;

        public IConnectiveRoom ConnectiveRoom { get; }

        public FixedConnectionRoomStrategy(InteriorRoom sharedRoom, IWebRequestController webRequests, string currentAdapterAddress)
        {
            this.webRequests = webRequests;
            this.currentAdapterAddress = currentAdapterAddress;

            ConnectiveRoom = new ConnectiveRoom(
                sharedRoom,
                static _ => UniTask.CompletedTask,
                RunConnectCycleStepAsync
            );
        }

        private async UniTask RunConnectCycleStepAsync(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, CancellationToken token)
        {
            if (ConnectiveRoom.CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await connectToRoomAsyncDelegate(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            string metadata = FixedMetadata.Default.ToJson();
            GenericPostRequest result = await webRequests.SignedFetchPostAsync(currentAdapterAddress, metadata, token);
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
