using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    public class ChatConnectiveRoom : ConnectiveRoom, IActivatableConnectiveRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly URLAddress adapterAddress;

        public ChatConnectiveRoom(IWebRequestController webRequests, URLAddress adapterAddress)
        {
            this.webRequests = webRequests;
            this.adapterAddress = adapterAddress;
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
            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchGetAsync(adapterAddress, metadata, token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.adapter;
            ReportHub.WithReport(ReportCategory.COMMS_CHAT_HANDLER).Log($"String is: {connectionString}");
            return connectionString;
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

        public bool Activated { get; private set; }

        public async UniTask ActivateAsync()
        {
            if (Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already activated");
                return;
            }

            Activated = true;
            await this.StartIfNotAsync();
        }

        public async UniTask DeactivateAsync()
        {
            if (!Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already deactivated");
                return;
            }

            Activated = false;
            await this.StopIfNotAsync();
        }
    }
}
