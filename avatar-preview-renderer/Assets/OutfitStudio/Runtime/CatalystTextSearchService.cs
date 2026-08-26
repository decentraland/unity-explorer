using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Services;
using UnityEngine.Networking;

namespace OutfitStudio
{
    /// <summary>
    /// Tag-aware text search against the catalyst content-server's lambdas collections endpoint
    /// (GET /lambdas/collections/wearables or /emotes, ?textSearch=...).
    ///
    /// This exists because marketplace-api's own <c>/v2/catalog?search=</c> (used by
    /// <see cref="CatalogService"/> for the actual browse/filter pass) only matches item name and
    /// description - it has no notion of tags. The lambdas endpoint indexes each item's full
    /// <c>data.tags</c> array, so a query like "jacket" also matches an item named "Black Jacket"
    /// or one whose name doesn't contain the word at all but is tagged with it.
    ///
    /// Builds <see cref="CatalogItem"/>s directly from the lambdas payload (name via i18n,
    /// thumbnail, rarity, slot, bodyShapes) rather than hydrating through marketplace-api's
    /// <see cref="CatalogQuery.Urns"/> lookup - that lookup only resolves collections-v2 (Polygon)
    /// URNs and silently returns zero results for legacy collections-v1 (Ethereum) items, which the
    /// lambdas endpoint (and marketplace-api's own name search) both cover fine. Fields marketplace-
    /// api alone carries (price, on-sale status, exact listing dates) aren't available here, so
    /// results built this way sort last under price/date-based sorts - acceptable since this is a
    /// supplementary discovery path, not the primary browse.
    /// </summary>
    public static class CatalystTextSearchService
    {
        // Observed to be accepted comfortably by the live endpoint; keeps each round trip small
        // while still reaching a reasonable cap in a handful of requests.
        private const int PAGE_SIZE = 200;

        public static void SearchItems(string category, string textSearch, int cap,
            Action<List<CatalogItem>> onSuccess, Action<string> onError)
        {
            // The lambdas endpoint 400s below 3 characters; a debounced keystroke can still land
            // here mid-type, so just report no matches instead of firing a request doomed to fail.
            if (string.IsNullOrEmpty(textSearch) || textSearch.Trim().Length < 3)
            {
                onSuccess?.Invoke(new List<CatalogItem>());
                return;
            }

            var isEmote = category == "emote";
            var itemsKey = isEmote ? "emotes" : "wearables";
            var endpoint = $"https://peer.decentraland.{APIService.Environment}/lambdas/collections/{itemsKey}";
            var results = new List<CatalogItem>();
            var seenUrns = new HashSet<string>();

            void FetchNext(string lastId)
            {
                var pageLimit = Math.Min(PAGE_SIZE, cap - results.Count);
                var url = $"{endpoint}?textSearch={UnityWebRequest.EscapeURL(textSearch)}&limit={pageLimit}";
                if (!string.IsNullOrEmpty(lastId))
                    url += $"&lastId={UnityWebRequest.EscapeURL(lastId)}";

                var request = UnityWebRequest.Get(url);
                var operation = request.SendWebRequest();

                operation.completed += _ =>
                {
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            onError?.Invoke($"{request.error} ({url})");
                            return;
                        }

                        var response = JObject.Parse(request.downloadHandler.text);
                        if (response[itemsKey] is not JArray items)
                        {
                            onError?.Invoke($"Unexpected lambdas response ({url})");
                            return;
                        }

                        string last = null;
                        foreach (var item in items.OfType<JObject>())
                        {
                            var urn = item["id"]?.Value<string>();
                            if (!string.IsNullOrEmpty(urn) && seenUrns.Add(urn))
                                results.Add(ToCatalogItem(item, urn, isEmote));
                            if (!string.IsNullOrEmpty(urn)) last = urn;
                        }

                        var hasMore = response["pagination"]?["next"] != null;
                        if (!hasMore || items.Count == 0 || results.Count >= cap)
                        {
                            onSuccess?.Invoke(results);
                            return;
                        }

                        FetchNext(last);
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

            FetchNext(null);
        }

        private static CatalogItem ToCatalogItem(JObject item, string urn, bool isEmote)
        {
            var displayName = (item["i18n"] as JArray)?.FirstOrDefault()?["text"]?.Value<string>()
                               ?? item["name"]?.Value<string>()
                               ?? urn;

            var dataObj = isEmote ? item["emoteDataADR74"] as JObject : item["data"] as JObject;
            var slotCategory = dataObj?["category"]?.Value<string>();
            var bodyShapes = ExtractBodyShapes(dataObj);

            var catalogItem = new CatalogItem
            {
                urn = urn,
                name = displayName,
                thumbnail = item["thumbnail"]?.Value<string>(),
                category = isEmote ? "emote" : "wearable",
                rarity = item["rarity"]?.Value<string>()
            };

            if (isEmote)
                catalogItem.data = new CatalogItem.ItemData
                    { emote = new CatalogItem.EmoteData { category = slotCategory, bodyShapes = bodyShapes } };
            else
                catalogItem.data = new CatalogItem.ItemData
                    { wearable = new CatalogItem.WearableData { category = slotCategory, bodyShapes = bodyShapes } };

            return catalogItem;
        }

        /// <summary>
        /// Representations carry full body-shape URNs (e.g. "urn:...:BaseMale"); this reduces them
        /// to the short "BaseMale"/"BaseFemale" forms CatalogItem's gender filtering already expects.
        /// </summary>
        private static string[] ExtractBodyShapes(JObject dataObj)
        {
            var shapes = new HashSet<string>();
            foreach (var rep in (dataObj?["representations"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
            {
                foreach (var shape in (rep["bodyShapes"] as JArray) ?? new JArray())
                {
                    var value = shape.Value<string>() ?? "";
                    if (value.Contains("BaseMale")) shapes.Add("BaseMale");
                    if (value.Contains("BaseFemale")) shapes.Add("BaseFemale");
                }
            }
            return shapes.ToArray();
        }
    }
}
