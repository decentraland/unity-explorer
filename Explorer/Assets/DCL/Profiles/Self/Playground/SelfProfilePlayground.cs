#if UNITY_EDITOR

using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Utility;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles.Self.Playground
{
    public class SelfProfilePlayground : MonoBehaviour
    {
        [SerializeField] private string url = "https://peer-eu1.decentraland.org/lambdas/";

        [ContextMenu(nameof(Start))]
        public void Start()
        {
            ExecuteAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid ExecuteAsync(CancellationToken ct)
        {
            var web3IdentityCache = new IWeb3IdentityCache.Default();

            var world = World.Create();
            var playerEntity = world.Create();

            var realmData = new RealmData(
                new LogIpfsRealm(
                    new IpfsRealm(URLDomain.FromString(url),
                        new ServerAbout(
                            lambdas: new ContentEndpoint(url)
                        )
                    )
                )
            );

            var urlsSource = new DecentralandUrlsSource(DecentralandEnvironment.Zone, realmData, ILaunchMode.PLAY);

            SelfProfile selfProfile = new SelfProfile(
                new LogProfileRepository(
                    new RealmProfileRepository(
                        IWebRequestController.TEST,
                        new PublishIpfsEntityCommand(web3IdentityCache, IWebRequestController.TEST, urlsSource, realmData),
                        urlsSource,
                        new DefaultProfileCache(),
                        new ProfilesAnalytics(ProfilesDebug.Create(null, new EntitiesAnalyticsDebug(null)), IAnalyticsController.Null),
                        false)
                ),
                web3IdentityCache,
                new EquippedWearables(),
                new WearableStorage(),
                new MemoryEmotesStorage(),
                new EquippedEmotes(),
                null,
                new DefaultProfileCache(),
                world,
                playerEntity
            );

            var profile = await selfProfile.ProfileAsync(ct);
            ReportHub.Log(ReportData.UNSPECIFIED, $"Profile is found {profile != null}");
            await selfProfile.UpdateProfileAsync(ct, updateAvatarInWorld: false);
            ReportHub.Log(ReportData.UNSPECIFIED, $"Profile is published successfully");
        }
    }
}

#endif
