using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem.Global;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Scripting;

namespace Global.Dynamic
{
    public class WorldManifestProvider
    {
        private readonly IWebRequestController webRequestController;

        private static URLAddress ORG_MANIFEST_URL = URLAddress.FromString("https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json");
        private static URLAddress ZONE_MANIFEST_URL = URLAddress.FromString("https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json");
        private static readonly string[] MAIN_REALM_NAMES = { "main", "shiva", "hela", "heimdallr", "baldr", "artemis", "loki", "dg", "hephaestus", "unicorn", "marvel", "nftworld" };
        private const string dclWorldName = "dcl.eth";

        private WorldManifest? cachedMainManifest;
        private UniTask<WorldManifest>? inFlightMainManifest;

        public WorldManifestProvider(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        // Idempotent while a fetch is pending; Preserve()d for multi-await. A completed task
        // may hold WorldManifest.Empty from a cancelled or failed fetch, so it is replaced
        // rather than reused.
        public void PrefetchGenesisManifest(DecentralandEnvironment environment, CancellationToken ct)
        {
            if (cachedMainManifest.HasValue) return;
            if (inFlightMainManifest is { Status: UniTaskStatus.Pending }) return;

            inFlightMainManifest = FetchGenesisManifestAsync(environment, ct).Preserve();
        }

        public async UniTask<WorldManifest> FetchWorldManifestAsync(URLDomain assetBundleRegistry, string realmName, DecentralandEnvironment environment, CancellationToken ct)
        {
            try
            {
                if(MAIN_REALM_NAMES.Contains(realmName))
                {
                    PrefetchGenesisManifest(environment, ct);
                    return await inFlightMainManifest!.Value; // set by PrefetchGenesisManifest above
                }

                if(realmName.EndsWith(dclWorldName))
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

        private async UniTask<WorldManifest> FetchGenesisManifestAsync(DecentralandEnvironment environment, CancellationToken ct)
        {
            try
            {
                if (cachedMainManifest.HasValue)
                    return cachedMainManifest.Value;

                URLAddress manifestURL = environment == DecentralandEnvironment.Zone ? ZONE_MANIFEST_URL : ORG_MANIFEST_URL;

                string? result = await webRequestController
                                      .GetAsync(new CommonArguments(manifestURL), ct,
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
