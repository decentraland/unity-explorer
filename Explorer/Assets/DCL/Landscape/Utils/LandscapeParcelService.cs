using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using Unity.Mathematics;

namespace DCL.Landscape.Utils
{
    public sealed class Int2Converter : JsonConverter<int2[]>
    {
        public override void WriteJson(JsonWriter writer, int2[]? value,
            JsonSerializer serializer)
        {
            var array = new JArray();

            if (value != null)
                foreach (int2 vector in value)
                    array.Add($"{vector.x},{vector.y}");

            array.WriteTo(writer);
        }

        public override int2[] ReadJson(JsonReader reader, Type objectType,
            int2[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);

            return array.Select(item =>
                         {
                             string[]? parts = item.ToString().Split(',');
                             return new int2(int.Parse(parts[0]), int.Parse(parts[1]));
                         })
                        .ToArray();
        }
    }

    public struct ParcelManifest
    {
        public int2[] roads;
        public int2[] occupied;
        public int2[] empty;
    }

    public struct FetchParcelResult
    {
        public bool Succeeded;
        public ParcelManifest Manifest;

        public static FetchParcelResult Empty() =>
            new () { Succeeded = false };
    }

    public class LandscapeParcelService
    {
        private const string ORG_MANIFEST_URL = "https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json";
        private const string ZONE_MANIFEST_URL = "https://places-e22845c.s3.us-east-1.amazonaws.com/WorldManifest.json";

        private readonly IWebRequestController webRequestController;
        private readonly string currentManifestURL;

        public LandscapeParcelService(IWebRequestController webRequestController, bool isZone)
        {
            currentManifestURL = isZone ? ZONE_MANIFEST_URL : ORG_MANIFEST_URL;
            this.webRequestController = webRequestController;
        }

        public async UniTask<FetchParcelResult> LoadManifestAsync(CancellationToken ct)
        {
            try
            {
                var result = await webRequestController
                    .GetAsync(new CommonArguments(URLAddress.FromString(currentManifestURL)), ct,
                        ReportCategory.LANDSCAPE)
                                                           .StoreTextAsync();

                if (result != null)
                {
                    var settings = new JsonSerializerSettings();
                    settings.Converters.Add(new Int2Converter());
                    ParcelManifest manifest = JsonConvert.DeserializeObject<ParcelManifest>(result, settings);

                    return new FetchParcelResult { Succeeded = true, Manifest = manifest };
                }
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.LANDSCAPE); }

            return FetchParcelResult.Empty();
        }
    }
}
