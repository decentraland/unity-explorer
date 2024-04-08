using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Ipfs;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System.Threading;
using UnityEngine;

namespace DCL.Profiles.Self.Playground
{
    public class SelfProfilePlayground : MonoBehaviour
    {
        [SerializeField] private string url = "https://peer-eu1.decentraland.org/lambdas/";

        private void Start()
        {
            ExecuteAsync(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid ExecuteAsync(CancellationToken ct)
        {
            var web3IdentityCache = new IWeb3IdentityCache.Default();

            var profiles = new SelfProfile(
                new RealmProfileRepository(
                    IWebRequestController.DEFAULT,
                    new RealmData(
                        new IpfsRealm(
                            web3IdentityCache,
                            IWebRequestController.DEFAULT,
                            URLDomain.FromString(url),
                            new ServerAbout(
                                lambdas: new ContentEndpoint(url)
                            )
                        )
                    ),
                    new DefaultProfileCache()
                ),
                web3IdentityCache,
                new EquippedWearables(),
                new WearableCatalog()
            );

            var profile = await profiles.ProfileAsync(ct);
            Debug.Log($"Profile is found {profile != null}");
            await profiles.PublishAsync(ct);
            Debug.Log($"Profile is published successfully");
        }
    }
}
