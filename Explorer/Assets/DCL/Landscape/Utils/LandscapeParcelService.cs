using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Utils
{
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

    public struct ParcelManifest
    {
        public Vector2[] roads;
        public Vector2[] occupied;
        public Vector2[] empty;

        public NativeParallelHashSet<int2> GetOwnedParcels()
        {
            var hashSet = new NativeParallelHashSet<int2>(occupied.Length, Allocator.Persistent);

            foreach (Vector2 parcel in occupied)
                hashSet.Add(new int2(parcel));

            return hashSet;
        }

        public NativeList<int2> GetEmptyParcels()
        {
            var nativeList = new NativeList<int2>(empty.Length, Allocator.Persistent);

            foreach (Vector2 emptyParcel in empty)
                nativeList.Add(new int2(emptyParcel));

            return nativeList;
        }
    }

    public struct FetchParcelResult
    {
        public bool Succeeded;
        public string Checksum;
        public ParcelManifest Manifest;

        public static FetchParcelResult Empty() =>
            new () { Succeeded = false, Checksum = string.Empty };
    }

    public class LandscapeParcelService
    {
        private const string MANIFEST_URL = "https://places-dcf8abb.s3.amazonaws.com/WorldManifest.json";
        private readonly IWebRequestController webRequestController;

        public LandscapeParcelService(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<FetchParcelResult> LoadManifest(CancellationToken ct)
        {
            try
            {
                string? result = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(MANIFEST_URL)), ct, ReportCategory.LANDSCAPE)
                                                           .StoreTextAsync();

                if (result != null)
                {
                    string checksum = ComputeSha256Hash(result);

                    var settings = new JsonSerializerSettings();
                    settings.Converters.Add(new Vector2Converter());
                    ParcelManifest manifest = JsonConvert.DeserializeObject<ParcelManifest>(result, settings);

                    return new FetchParcelResult { Succeeded = true, Checksum = checksum, Manifest = manifest };
                }
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.LANDSCAPE); }

            return FetchParcelResult.Empty();
        }

        // checksum generation, created by chat-gpt
        private string ComputeSha256Hash(string rawData)
        {
            using var sha256Hash = SHA256.Create();

            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            var builder = new StringBuilder();

            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));

            return builder.ToString();
        }
    }
}
