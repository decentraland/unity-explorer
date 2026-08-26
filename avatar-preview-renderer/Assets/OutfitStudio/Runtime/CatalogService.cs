using System;
using System.Collections.Generic;
using System.Text;
using Services;
using UnityEngine;
using UnityEngine.Networking;

namespace OutfitStudio
{
    /// <summary>
    /// Browses the Decentraland marketplace catalog (GET /v2/catalog).
    ///
    /// /v2/catalog is what the web marketplace itself browses, and it's the only variant whose sale
    /// data can be trusted: /v1/items reports a stale primary price and on-sale flag (verified:
    /// "Donald Dump" comes back from /v1/items as isOnSale=false, price 0, while /v2/catalog reports
    /// it mintable at 30 MANA with an open 50 MANA listing), and /v1/catalog misses newer
    /// trade-based listings in its own aggregates.
    ///
    /// Callback-based (instead of Awaitable) so it works both in play mode and in the editor
    /// (the Outfit Studio window browses the catalog without entering play mode).
    /// Uses the same environment switch (org/zone) as <see cref="APIService"/>.
    /// </summary>
    public static class CatalogService
    {
        // Largest page size the endpoint serves efficiently in one round trip (verified against
        // the live API - first=1000 returns promptly; there's no documented hard cap below that).
        private const int MAX_FETCH_PAGE = 1000;

        private static string EndpointItems =>
            $"https://marketplace-api.decentraland.{APIService.Environment}/v2/catalog";

        public static void Search(CatalogQuery query, Action<CatalogPage> onSuccess, Action<string> onError)
        {
            var url = BuildUrl(query);
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

                    var page = JsonUtility.FromJson<CatalogPage>(request.downloadHandler.text);

                    if (page?.data == null)
                    {
                        onError?.Invoke($"Unexpected catalog response ({url})");
                        return;
                    }

                    onSuccess?.Invoke(page);
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

        /// <summary>
        /// Fetches every item matching <paramref name="query"/>'s filters (ignoring its Skip/First),
        /// up to <paramref name="cap"/> items, across as many <see cref="Search"/> calls as needed.
        /// The live marketplace-api ignores <c>sortBy</c> entirely, so there is no server-side
        /// ordering to rely on for pagination; fetching the whole (capped) filtered set and sorting
        /// it client-side is the only way to get a globally-correct order instead of one that's only
        /// correct within whatever page the server happened to return.
        /// </summary>
        public static void SearchAll(CatalogQuery query, int cap, Action<CatalogItem[], int> onSuccess,
            Action<string> onError)
        {
            var pageQuery = new CatalogQuery
            {
                Category = query.Category,
                Search = query.Search,
                WearableCategory = query.WearableCategory,
                EmoteCategory = query.EmoteCategory,
                Rarity = query.Rarity,
                Gender = query.Gender,
                IsOnSale = query.IsOnSale,
                OnlyMinting = query.OnlyMinting,
                SortBy = query.SortBy,
                Urns = query.Urns,
                ContractAddress = query.ContractAddress,
                First = Mathf.Min(MAX_FETCH_PAGE, cap),
                Skip = 0
            };
            var accumulated = new List<CatalogItem>();

            void FetchNext()
            {
                Search(pageQuery, page =>
                {
                    accumulated.AddRange(page.data);

                    var doneFetching = page.data.Length < pageQuery.First || accumulated.Count >= cap ||
                                        accumulated.Count >= page.total;
                    if (doneFetching)
                    {
                        onSuccess?.Invoke(accumulated.ToArray(), page.total);
                        return;
                    }

                    pageQuery.Skip += pageQuery.First;
                    pageQuery.First = Mathf.Min(MAX_FETCH_PAGE, cap - accumulated.Count);
                    FetchNext();
                }, onError);
            }

            FetchNext();
        }

        private static string BuildUrl(CatalogQuery query)
        {
            var sb = new StringBuilder(EndpointItems);

            sb.AppendFormat("?first={0}&skip={1}", query.First, query.Skip);

            if (query.Urns is { Length: > 0 })
            {
                // Direct URN lookup ignores the browse filters
                foreach (var urn in query.Urns)
                    sb.AppendFormat("&urn={0}", UnityWebRequest.EscapeURL(urn));

                return sb.ToString();
            }

            if (!string.IsNullOrEmpty(query.ContractAddress))
                sb.AppendFormat("&contractAddress={0}", query.ContractAddress);
            if (!string.IsNullOrEmpty(query.Category))
                sb.AppendFormat("&category={0}", query.Category);
            if (!string.IsNullOrEmpty(query.Search))
                sb.AppendFormat("&search={0}", UnityWebRequest.EscapeURL(query.Search));
            if (!string.IsNullOrEmpty(query.WearableCategory))
                sb.AppendFormat("&wearableCategory={0}", query.WearableCategory);
            if (!string.IsNullOrEmpty(query.EmoteCategory))
                sb.AppendFormat("&emoteCategory={0}", query.EmoteCategory);
            if (!string.IsNullOrEmpty(query.Rarity))
                sb.AppendFormat("&rarity={0}", query.Rarity);
            if (!string.IsNullOrEmpty(query.Gender))
            {
                if (query.Category == "emote")
                    sb.AppendFormat("&emoteGender={0}", query.Gender);
                else
                    sb.AppendFormat("&wearableGender={0}", query.Gender);
            }
            // Only ever sent as true, never as false: the endpoint treats isOnSale=false as its own
            // filter ("only items that are NOT on sale") rather than as "don't filter", so an off
            // toggle has to omit the param to mean "everything". Filtering server-side is what makes
            // the toggle match the web marketplace: it selects mintable items OR items with open
            // listings, which is wider than any single field on the response (a sold-out item with
            // 1000 open listings still reports isOnSale=false but is very much on sale).
            if (query.IsOnSale)
                sb.Append("&isOnSale=true");
            // Narrows the on-sale set to primary sales only - items still mintable from the creator's
            // collection, with listing-only (secondary) sales removed. Verified against the live API:
            // wearables drop 6760 -> 4102 with it on, and every item returned is mintable with zero
            // open listings (an unknown param would have been ignored and left the total unchanged).
            if (query.OnlyMinting)
                sb.Append("&onlyMinting=true");

            if (!string.IsNullOrEmpty(query.SortBy))
                sb.AppendFormat("&sortBy={0}", query.SortBy);

            return sb.ToString();
        }
    }
}
