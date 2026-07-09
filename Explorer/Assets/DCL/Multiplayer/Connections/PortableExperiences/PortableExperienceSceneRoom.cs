using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.PortableExperiences
{
    /// <summary>
    ///     A scene-level LiveKit comms connection to a Portable Experience world's scene room
    ///     (<c>worlds/{realm}/scenes/{sceneId}/comms</c>). The join carries the <c>sceneId</c>, which is what the
    ///     world-content-server's room-event-processor needs to spawn the authoritative server for the experience.
    ///     Unlike <c>GateKeeperSceneRoom</c> it always targets the world scene endpoint: a PX realm is configured with an
    ///     empty WorldManifest (so <c>RealmData.SingleScene == true</c>), which would otherwise route comms to the
    ///     world-level room that carries no sceneId.
    /// </summary>
    public class PortableExperienceSceneRoom : ConnectiveRoom
    {
        private readonly IWebRequestController webRequests;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IRealmData realmData;
        private readonly string sceneId;

        public PortableExperienceSceneRoom(IWebRequestController webRequests, IWeb3IdentityCache identityCache, IDecentralandUrlsSource urlsSource, IRealmData realmData, string sceneId)
        {
            this.webRequests = webRequests;
            this.identityCache = identityCache;
            this.urlsSource = urlsSource;
            this.realmData = realmData;
            this.sceneId = sceneId;
        }

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            // Skip if identity is not available (e.g., during sign-out).
            if (identityCache.Identity == null)
                return;

            if (CurrentState() is not IConnectiveRoom.State.Running
                || Room().Info.ConnectionState != LKConnectionState.ConnConnected)
            {
                string connectionString = await ConnectionStringAsync(token);
                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            string url = string.Format(urlsSource.Url(DecentralandUrl.WorldCommsAdapter), realmData.RealmName, sceneId);
            var meta = new MetaData(sceneId, Vector2Int.zero, new MetaData.Input(realmData.RealmName, Vector2Int.zero));

            ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"[PortableExperienceSceneRoom] Requesting adapter from '{url}' for scene '{sceneId}'");

            AdapterResponse response = await webRequests
                                            .SignedFetchPostAsync(url, meta.BuildWithSecret(realmData.WorldCommsSecret), token)
                                            .CreateFromJson<AdapterResponse>(WRJsonParser.Unity);

            return string.IsNullOrEmpty(response.adapter) ? response.fixedAdapter : response.adapter;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
            public string fixedAdapter;
        }
    }
}
