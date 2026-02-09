using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.PluginSystem.Global;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle.Realm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Scripting;

namespace Global.Dynamic
{
    public class WorldManifestProvider
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWebRequestController webRequestController;
        private readonly AssetReferenceT<ParcelData> parsedParcels;

        private static URLAddress ORG_MANIFEST_URL = URLAddress.FromString("https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json");
        private static URLAddress ZONE_MANIFEST_URL = URLAddress.FromString("https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json");
        private static string mainRealmName = "main";
        private static string dclWorldName = "dcl.eth";

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

        public async UniTask<WorldManifest> FetchWorldManifestAsync(URLDomain assetBundleRegistry, string realmName, bool isZone, CancellationToken ct)
        {
            try
            {
                if(realmName.StartsWith(mainRealmName))
                    return await FetchGenesisManifestAsync(realmName, isZone, ct);

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

        private async Task<WorldManifest> FetchNonGenesisManifestAsync(URLDomain assetBundleRegistry, string worldURL, CancellationToken ct)
        {
            var result = await webRequestController
                              .GetAsync(new CommonArguments(assetBundleRegistry.Append(URLPath.FromString($"worlds/{worldURL}/manifest"))), ct,
                                   ReportCategory.REALM)
                              .StoreTextAsync();

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new Vector2Converter());
            WorldManifest raw = JsonConvert.DeserializeObject<WorldManifest>(result, settings);
            return WorldManifest.WithParsedSets(raw);
        }

        private async UniTask<WorldManifest> FetchGenesisManifestAsync(string realmURL, bool isZone, CancellationToken ct)
        {
            if (cachedMainManifest.HasValue)
                return cachedMainManifest.Value;

            ProvidedAsset<ParcelData> fallbackParcelData = await assetsProvisioner.ProvideMainAssetAsync(parsedParcels, ct);
            URLAddress manifestURL = URLAddress.EMPTY;

            if (isZone)
                manifestURL = ZONE_MANIFEST_URL;
            else
                manifestURL = ORG_MANIFEST_URL;

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

            return new  WorldManifest(fallbackParcelData.Value.ownedParcels,
                                          fallbackParcelData.Value.emptyParcels,
                                          fallbackParcelData.Value.roadParcels);
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
