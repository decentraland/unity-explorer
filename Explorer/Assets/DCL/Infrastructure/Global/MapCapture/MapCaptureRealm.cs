using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS;
using Global.Dynamic;
using System.Threading;

namespace Global.MapCapture
{
    /// <summary>
    ///     Configures the realm data for Genesis City the way the realm controller does, without the global world,
    ///     pointer loading or comms that come with it. The world manifest is what terrain generation needs.
    /// </summary>
    public static class MapCaptureRealm
    {
        private const string ABOUT_PATH = "/about";
        private const string DEFAULT_COMMS_PROTOCOL = "v3";

        public static async UniTask ConfigureGenesisAsync(RealmData realmData, IWebRequestController webRequestController, IDecentralandUrlsSource urls,
            WorldManifestProvider manifestProvider, DecentralandEnvironment environment, CancellationToken ct)
        {
            URLDomain realm = URLDomain.FromString(urls.Url(DecentralandUrl.Genesis));

            var about = new ServerAbout();
            await webRequestController.GetAsync(new CommonArguments(realm.Append(new URLPath(ABOUT_PATH))), ct, ReportCategory.REALM).OverwriteFromJsonAsync(about, WRJsonParser.Unity);

            string realmName = about.configurations.realmName.EnsureNotNull("Realm name not found");
            WorldManifest manifest = await manifestProvider.FetchWorldManifestAsync(URLDomain.FromString(urls.Url(DecentralandUrl.AssetBundleRegistry)), realmName, environment, ct);

            realmData.Reconfigure(
                new IpfsRealm(realm, about),
                realmName,
                about.configurations.networkId,
                string.Empty,
                about.comms?.protocol ?? DEFAULT_COMMS_PROTOCOL,
                realm.Value,
                false,
                manifest,
                null,
                realm);
        }
    }
}
