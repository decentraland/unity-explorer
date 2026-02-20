using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Proto;
using LiveKit.Rooms;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    public class ChatConnectiveRoom : ConnectiveRoom, IActivatableConnectiveRoom
    {
        private static readonly TimeSpan CONNECTION_UPDATE_INTERVAL = TimeSpan.FromSeconds(5);

        private readonly IWebRequestController webRequests;
        private readonly URLAddress adapterAddress;

        public bool Activated { get; private set; }

        public ChatConnectiveRoom(IWebRequestController webRequests, URLAddress adapterAddress)
        {
            this.webRequests = webRequests;
            this.adapterAddress = adapterAddress;

            Room().ConnectionUpdated += OnConnectionUpdated;
        }

        public new void Dispose()
        {
            Room().ConnectionUpdated -= OnConnectionUpdated;
            base.Dispose();
        }

        private void OnConnectionUpdated(IRoom room1, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason)
        {
            if (connectionUpdate == ConnectionUpdate.Disconnected && CurrentState() == IConnectiveRoom.State.Running)
                DisconnectCurrentRoomAsync(false, CancellationToken.None).Forget();
        }

        public async UniTask ActivateAsync()
        {
            if (Activated)
                return;

            Activated = true;
            await this.StartIfNotAsync();
        }

        public async UniTask DeactivateAsync()
        {
            if (!Activated)
                return;

            Activated = false;
            await this.StopIfNotAsync();
        }

        protected override UniTask PrewarmAsync(CancellationToken token)
        {
            SendConnectionStatusAsync(token).Forget();
            return UniTask.CompletedTask;
        }

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        private async UniTaskVoid SendConnectionStatusAsync(CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false)
            {
                if (CurrentState() == IConnectiveRoom.State.Running)
                    ((InteriorRoom)Room()).SimulateConnectionStateChanged();

                await UniTask.Delay(CONNECTION_UPDATE_INTERVAL, cancellationToken: ct);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken ct)
        {
            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchGetAsync(adapterAddress, metadata, ct);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            return response.adapter;
        }

        [Serializable]
        private struct FixedMetadata
        {
            public static FixedMetadata Default = new ()
            {
                signer = "dcl:explorer",
            };

            public string signer;

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
