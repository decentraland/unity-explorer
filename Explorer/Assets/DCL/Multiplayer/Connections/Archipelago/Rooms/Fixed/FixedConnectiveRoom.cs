using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.PrivateWorlds;
using DCL.Web3.Identities;
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
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWorldCommsSecret? worldCommsSecret;

        public FixedConnectiveRoom(IWebRequestController webRequests, ICurrentAdapterAddress currentAdapterAddress, IWeb3IdentityCache identityCache,
            IWorldCommsSecret? worldCommsSecret = null)
        {
            this.webRequests = webRequests;
            this.currentAdapterAddress = currentAdapterAddress;
            this.identityCache = identityCache;
            this.worldCommsSecret = worldCommsSecret;
        }

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            // Skip if identity is not available (e.g., during sign-out)
            if (identityCache.Identity == null)
                return;

            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();
            string metadata = BuildMetadata();
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"FixedRoom POST to {adapterUrl} with metadata: {metadata}");
            var result = webRequests.SignedFetchPostAsync(adapterUrl, metadata, token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.fixedAdapter;
            ReportHub.WithReport(ReportCategory.COMMS_SCENE_HANDLER).Log($"String is: {connectionString}");
            return connectionString;
        }

        private string BuildMetadata()
        {
            string? secret = worldCommsSecret?.Secret;

            if (string.IsNullOrEmpty(secret))
                return FixedMetadata.Default.ToJson();

            var metadata = new FixedMetadataWithSecret
            {
                intent = "dcl:explorer:comms-handshake",
                signer = "dcl:explorer",
                isGuest = false,
                secret = secret,
            };

            return JsonUtility.ToJson(metadata);
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
        private struct FixedMetadataWithSecret
        {
            public string intent;
            public string signer;
            public bool isGuest;
            public string secret;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string fixedAdapter;
        }
    }
}
