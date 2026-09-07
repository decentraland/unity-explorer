using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;

namespace Global.Dynamic
{
    public class WorldManifestProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private static readonly URLAddress ORG_MANIFEST_URL = URLAddress.FromString("https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json");
        private static readonly URLAddress ZONE_MANIFEST_URL = URLAddress.FromString("https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json");
        private static readonly string[] MAIN_REALM_NAMES = { "main", "shiva", "hela", "heimdallr", "baldr", "artemis", "loki", "dg", "hephaestus", "unicorn", "marvel", "nftworld" };
        private const string DCL_WORLD_NAME = "dcl.eth";

        private WorldManifest? cachedMainManifest;

        public WorldManifestProvider(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<WorldManifest> FetchWorldManifestAsync(URLDomain assetBundleRegistry, string realmName, DecentralandEnvironment environment, bool realmIsGenesis, CancellationToken ct)
        {
            try
            {
                if (IsGenesisRealm(realmName, environment, realmIsGenesis))
                    return GenesisManifestUrl(environment) is { } genesisManifestUrl
                        ? await FetchGenesisManifestAsync(genesisManifestUrl, ct)
                        : WorldManifest.Empty;

                if(realmName.EndsWith(DCL_WORLD_NAME))
                    return await FetchNonGenesisManifestAsync(assetBundleRegistry, realmName, ct);

                //If its not Genesis or world, nothing we can do
                return WorldManifest.Empty;
            }
            catch (OperationCanceledException)
            {
                return WorldManifest.Empty;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"World manifest fetch failed for '{realmName}': {e.Message}");
                return WorldManifest.Empty;;
            }
        }

        /// <summary>
        ///     A world the registry has not indexed answers 404, and that is an ordinary outcome: the caller treats an
        ///     absent manifest as <see cref="WorldManifest.Empty" /> and resolves the world's scenes from its
        ///     <c>scenesUrn</c> instead. Retrying cannot turn that 404 into a manifest, so the request takes neither
        ///     the default three attempts — which delay every such realm change by the full backoff — nor the error
        ///     report they raise on the way.
        /// </summary>
        private async UniTask<WorldManifest> FetchNonGenesisManifestAsync(URLDomain assetBundleRegistry, string worldURL, CancellationToken ct)
        {
            Result<string> result = await webRequestController
                                         .GetAsync(new CommonArguments(assetBundleRegistry.Append(URLPath.FromString($"worlds/{worldURL}/manifest")), RetryPolicy.NONE), ct,
                                              ReportCategory.REALM)
                                         .StoreTextAsync()
                                         .SuppressToResultAsync();

            if (!result.Success)
                return WorldManifest.Empty;

            try
            {
                WorldManifestDto dto = JsonConvert.DeserializeObject<WorldManifestDto>(result.Value);
                return WorldManifest.Create(dto);
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"World manifest for '{worldURL}' could not be parsed: {e.Message}");
                return WorldManifest.Empty;
            }
        }

        /// <summary>
        ///     The decentraland environments recognize their genesis realms by name. A <c>--base-domain</c> deployment
        ///     names its realms freely, so there genesis is the realm's classification — no fixed scene urns, the same
        ///     rule <see cref="RealmData.Reconfigure" /> applies — and the name list stays out of it.
        /// </summary>
        private static bool IsGenesisRealm(string realmName, DecentralandEnvironment environment, bool realmIsGenesis) =>
            environment == DecentralandEnvironment.Custom ? realmIsGenesis : MAIN_REALM_NAMES.Contains(realmName);

        /// <summary>
        ///     Where the Genesis City manifest lives. For the decentraland environments it is a static S3 artifact
        ///     describing decentraland's own Genesis City. A <c>--base-domain</c> deployment's genesis realm is a
        ///     different city, so decentraland's artifact never applies to it — Custom resolves the deployment's own
        ///     manifest from the base domain instead (<see cref="DecentralandUrl.GenesisWorldManifest" />).
        /// </summary>
        private URLAddress? GenesisManifestUrl(DecentralandEnvironment environment) =>
            environment switch
            {
                DecentralandEnvironment.Org => ORG_MANIFEST_URL,
                DecentralandEnvironment.Zone => ZONE_MANIFEST_URL,
                DecentralandEnvironment.Custom => URLAddress.FromString(decentralandUrlsSource.Url(DecentralandUrl.GenesisWorldManifest)),
                _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null),
            };

        private async UniTask<WorldManifest> FetchGenesisManifestAsync(URLAddress manifestUrl, CancellationToken ct)
        {
            try
            {
                if (cachedMainManifest.HasValue)
                    return cachedMainManifest.Value;

                string? result = await webRequestController
                                      .GetAsync(new CommonArguments(manifestUrl), ct,
                                           ReportCategory.REALM)
                                      .StoreTextAsync();

                if (string.IsNullOrEmpty(result))
                    return WorldManifest.Empty;

                var settings = new JsonSerializerSettings();
                WorldManifestDto dto = JsonConvert.DeserializeObject<WorldManifestDto>(result, settings);
                cachedMainManifest = WorldManifest.Create(dto, true);
                return cachedMainManifest.Value;

            }
            catch (OperationCanceledException)
            {
                return WorldManifest.Empty;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"World manifest fetch failed for genesis: {e.Message}");
                return WorldManifest.Empty;
            }
        }
    }

}
