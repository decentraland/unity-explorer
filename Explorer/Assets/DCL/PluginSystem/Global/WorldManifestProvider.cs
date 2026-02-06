using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Landscape.Config;
using DCL.PluginSystem.Global;
using DCL.WebRequests;
using ECS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Scripting;

namespace DCL.PluginSystem.Global
{
    public class WorldManifestProvider
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly AssetReferenceT<ParcelData> parsedParcels;

        private static URLAddress ORG_MANIFEST_URL = URLAddress.FromString("https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json");
        private static URLAddress ZONE_MANIFEST_URL = URLAddress.FromString("https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json");

        //Cached version of Genesis Manifest (either org or zone)
        private WorldManifest? cachedMainManifest;

        public WorldManifestProvider(
            IAssetsProvisioner assetsProvisioner,
            IWebRequestController webRequestController,
            AssetReferenceT<ParcelData> parsedParcels)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.webRequestController = webRequestController;
            this.parsedParcels = parsedParcels;
        }

        public async UniTask<WorldManifest> FetchWorldManifestAsync(URLDomain assetBundleRegistry, string realmURL, CancellationToken ct)
        {
            try
            {
                if(realmURL.StartsWith("https://realm-provider-ea.decentraland"))
                    return await FetchGenesisManifestAsync(realmURL, ct);
                else if(realmURL.Contains("dcl.eth"))
                    return await FetchNonGenesisManifestAsync(assetBundleRegistry, realmURL, ct);

                //If its not Genesis or world, nothing we can do
                return WorldManifest.Empty;
            }
            catch (OperationCanceledException)
            {
                return WorldManifest.Empty;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"World manifest fetch failed for '{realmURL}': {e.Message}");
                return WorldManifest.Empty;;
            }
        }

        private async Task<WorldManifest> FetchNonGenesisManifestAsync(URLDomain assetBundleRegistry, string worldURL, CancellationToken ct)
        {
            //TODO (JUANI): Can we just pass the dcl name here?

            string worldName = ExtractWorldNameFromUrl(worldURL);
            var result = await webRequestController
                              .GetAsync(new CommonArguments(assetBundleRegistry.Append(URLPath.FromString($"worlds/{worldName}/manifest"))), ct,
                                   ReportCategory.REALM)
                              .StoreTextAsync();

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Vector2Converter());
            WorldManifest raw = JsonConvert.DeserializeObject<WorldManifest>(result, settings);
            return WorldManifest.WithParsedSets(raw);
        }

        /// <summary>
        ///     Extracts the world name (e.g. pastrami.dcl.eth) from a full world URL.
        ///     Handles URLs like https://worlds-content-server.decentraland.zone/world/pastrami.dcl.eth or .../world/pastrami.dcl.eth/manifest.
        /// </summary>
        private static string ExtractWorldNameFromUrl(string worldURL)
        {
            const string WORLD_PATH_PREFIX = "/world/";
            int prefixIndex = worldURL.IndexOf(WORLD_PATH_PREFIX, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
                return worldURL;

            int start = prefixIndex + WORLD_PATH_PREFIX.Length;
            int end = worldURL.IndexOf('/', start);
            return end >= 0 ? worldURL.Substring(start, end - start) : worldURL.Substring(start);
        }

        private async UniTask<WorldManifest> FetchGenesisManifestAsync(string realmURL, CancellationToken ct)
        {
            if (cachedMainManifest.HasValue)
                return cachedMainManifest.Value;

            ProvidedAsset<ParcelData> fallbackParcelData = await assetsProvisioner.ProvideMainAssetAsync(parsedParcels, ct);
            URLAddress manifestURL = URLAddress.EMPTY;

            if (realmURL.Contains("org") || realmURL.Contains("today"))
                manifestURL = ORG_MANIFEST_URL;
            else
                manifestURL = ZONE_MANIFEST_URL;

            var result = await webRequestController
                              .GetAsync(new CommonArguments(manifestURL), ct,
                                   ReportCategory.REALM)
                              .StoreTextAsync();

            if (!string.IsNullOrEmpty(result))
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new Vector2Converter());
                WorldManifest raw = JsonConvert.DeserializeObject<WorldManifest>(result, settings);
                cachedMainManifest = WorldManifest.WithParsedSets(raw);
                return cachedMainManifest.Value;
            }
            else
            {
                return new  WorldManifest(fallbackParcelData.Value.ownedParcels,
                                          fallbackParcelData.Value.emptyParcels,
                                          fallbackParcelData.Value.roadParcels);
            }
        }
    }

    [Preserve]
    public class Vector2Converter : JsonConverter<Vector2[]>
    {
        public override void WriteJson(JsonWriter writer, Vector2[]? value, JsonSerializer serializer)
        {
            var array = new JArray();

            if (value != null)
                foreach (Vector2 vector in value)
                    array.Add($"{vector.x},{vector.y}");

            array.WriteTo(writer);
        }

        public override Vector2[] ReadJson(JsonReader reader, Type objectType, Vector2[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);

            return array.Select(item =>
                         {
                             string[]? parts = item.ToString().Split(',');
                             return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                         })
                        .ToArray();
        }
    }
}
