using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
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

        private static readonly URLAddress ORG_MANIFEST_URL = URLAddress.FromString("https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json");
        private static readonly URLAddress ZONE_MANIFEST_URL = URLAddress.FromString("https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json");
        private static readonly string[] MAIN_REALM_NAMES = { "main", "shiva", "hela", "heimdallr", "baldr", "artemis", "loki", "dg", "hephaestus", "unicorn", "marvel", "nftworld" };
        private const string DCL_WORLD_NAME = "dcl.eth";

        private WorldManifest? cachedMainManifest;

        public WorldManifestProvider(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<WorldManifest> FetchWorldManifestAsync(URLDomain assetBundleRegistry, string realmName, DecentralandEnvironment environment, CancellationToken ct)
        {
            try
            {
                if (MAIN_REALM_NAMES.Contains(realmName))
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

        private async UniTask<WorldManifest> FetchNonGenesisManifestAsync(URLDomain assetBundleRegistry, string worldURL, CancellationToken ct)
        {
            try
            {
                var result = await webRequestController
                                  .GetAsync(new CommonArguments(assetBundleRegistry.Append(URLPath.FromString($"worlds/{worldURL}/manifest"))), ct,
                                       ReportCategory.REALM)
                                  .StoreTextAsync();

                WorldManifestDto dto = JsonConvert.DeserializeObject<WorldManifestDto>(result);
                return WorldManifest.Create(dto);
            }
            catch (OperationCanceledException)
            {
                return WorldManifest.Empty;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"World manifest fetch failed for '{worldURL}': {e.Message}");
                return WorldManifest.Empty;
            }
        }

        /// <summary>
        ///     Where the Genesis City manifest lives, or null for an environment that has none. It is a static S3
        ///     artifact describing decentraland's own Genesis City, so a <c>--base-domain</c> deployment's realms are
        ///     not that city even when they reuse its realm names — it has no genesis manifest rather than a
        ///     differently-hosted one, and applying decentraland's would describe the wrong world.
        /// </summary>
        private static URLAddress? GenesisManifestUrl(DecentralandEnvironment environment) =>
            environment switch
            {
                DecentralandEnvironment.Org => ORG_MANIFEST_URL,
                DecentralandEnvironment.Today => ORG_MANIFEST_URL,
                DecentralandEnvironment.Zone => ZONE_MANIFEST_URL,
                DecentralandEnvironment.Custom => null,
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
