using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Services;
using UnityEngine.Networking;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Loads a Builder (draft) collection's items via the signed builder-api — the same endpoint
    /// the explorer's --self-preview-builder-collections flag uses. Each item is converted into
    /// a RawActiveEntity JSON (contents as {key, url} against the public storage endpoint) so it
    /// can be equipped through the renderer's existing base64 mechanism
    /// (EntityDefinition.FromBase64 / PreviewConfiguration.Base64).
    ///
    /// Published collections (0x contract addresses) don't come through here — they use the
    /// unauthenticated marketplace catalog (CatalogService with ContractAddress).
    /// </summary>
    public static class BuilderCollectionService
    {
        private static string EndpointItems(string collectionId) =>
            $"https://builder-api.decentraland.{APIService.Environment}/v1/collections/{collectionId}/items";

        private static string EndpointContents =>
            $"https://builder-api.decentraland.{APIService.Environment}/v1/storage/contents/";

        /// <summary>A draft collection item, ready to display and equip.</summary>
        public class DraftItem
        {
            public string Id;
            public string Name;
            public string Type; // "wearable" | "emote"
            public string Category; // wearable slot or emote category
            public string Rarity;
            public string ThumbnailUrl;
            public string[] BodyShapes; // urn:...BaseMale / BaseFemale

            /// <summary>Base64-encoded RawActiveEntity JSON (the renderer's builder-item format).</summary>
            public string Base64Entity;
        }

        public static void LoadDraftCollection(string collectionId, BuilderIdentity identity,
            Action<List<DraftItem>> onSuccess, Action<string> onError)
        {
            if (identity == null)
            {
                onError?.Invoke("No identity saved — paste your Builder identity first");
                return;
            }

            if (identity.IsExpired)
            {
                onError?.Invoke($"Identity expired {identity.Expiration:yyyy-MM-dd} — paste a fresh one from builder.decentraland.org");
                return;
            }

            var url = EndpointItems(collectionId);
            var request = UnityWebRequest.Get(url);

            foreach (var (key, value) in identity.SignedHeaders("get", new Uri(url).AbsolutePath))
                request.SetRequestHeader(key, value);

            var operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.responseCode == 401)
                    {
                        onError?.Invoke("Builder API rejected the identity (401) — paste a fresh one, and make sure the wallet has access to this collection");
                        return;
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke($"{request.error} ({url})");
                        return;
                    }

                    var response = JObject.Parse(request.downloadHandler.text);

                    if (response["ok"]?.Value<bool>() != true || response["data"] is not JArray items)
                    {
                        onError?.Invoke("Unexpected builder API response");
                        return;
                    }

                    onSuccess?.Invoke(items.Select(ToDraftItem).Where(item => item != null).ToList());
                }
                catch (Exception e)
                {
                    onError?.Invoke(e.Message);
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private static DraftItem ToDraftItem(JToken item)
        {
            var contents = item["contents"] as JObject;
            var data = item["data"] as JObject;

            if (contents == null || data == null) return null;

            var type = item["type"]?.Value<string>() ?? "wearable";
            var thumbnailFile = item["thumbnail"]?.Value<string>();
            var thumbnailHash = thumbnailFile != null ? contents[thumbnailFile]?.Value<string>() : null;

            var representations = (data["representations"] as JArray)?
                .OfType<JObject>()
                .ToList() ?? new List<JObject>();

            var draft = new DraftItem
            {
                Id = item["id"]?.Value<string>(),
                Name = item["name"]?.Value<string>() ?? "<unnamed>",
                Type = type,
                Category = data["category"]?.Value<string>(),
                Rarity = item["rarity"]?.Value<string>(),
                ThumbnailUrl = thumbnailHash != null ? EndpointContents + thumbnailHash : null,
                BodyShapes = representations
                    .SelectMany(r => (r["bodyShapes"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>())
                    .Distinct()
                    .ToArray(),
                Base64Entity = BuildBase64Entity(item, contents, data, representations, type, thumbnailFile)
            };

            return draft.Id == null ? null : draft;
        }

        /// <summary>
        /// Builds the RawActiveEntity JSON (see Assets/Scripts/Data/RawActiveEntity.cs) for this
        /// draft item: representation contents become {key, url} pairs pointing at the public
        /// builder storage endpoint. For emotes the payload goes under emoteDataADR74 and `data`
        /// is omitted (RawActiveEntity.IsEmote keys off an empty data.category).
        /// </summary>
        private static string BuildBase64Entity(JToken item, JObject contents, JObject data,
            List<JObject> representations, string type, string thumbnailFile)
        {
            var payload = new JObject
            {
                ["id"] = item["id"]?.Value<string>(),
                ["name"] = item["name"]?.Value<string>(),
                ["thumbnail"] = thumbnailFile
            };

            var body = new JObject
            {
                ["category"] = data["category"]?.Value<string>(),
                ["representations"] = new JArray(representations.Select(r => new JObject
                {
                    ["bodyShapes"] = r["bodyShapes"]?.DeepClone() ?? new JArray(),
                    ["mainFile"] = r["mainFile"]?.Value<string>(),
                    ["contents"] = new JArray(((r["contents"] as JArray)?.Values<string>() ?? Enumerable.Empty<string>())
                        .Where(file => file != thumbnailFile && contents[file] != null)
                        .Select(file => new JObject
                        {
                            ["key"] = file,
                            ["url"] = EndpointContents + contents[file]!.Value<string>()
                        })),
                    ["overrideHides"] = r["overrideHides"]?.DeepClone() ?? new JArray(),
                    ["overrideReplaces"] = r["overrideReplaces"]?.DeepClone() ?? new JArray()
                }))
            };

            if (type == "emote")
            {
                body["loop"] = data["loop"]?.Value<bool>() ?? false;
                payload["emoteDataADR74"] = body;
            }
            else
            {
                body["hides"] = data["hides"]?.DeepClone() ?? new JArray();
                body["replaces"] = data["replaces"]?.DeepClone() ?? new JArray();
                body["removesDefaultHiding"] = data["removesDefaultHiding"]?.DeepClone() ?? new JArray();
                payload["data"] = body;
            }

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None)));
        }
    }
}
