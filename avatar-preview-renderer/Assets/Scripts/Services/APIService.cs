using System;
using Data;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Utils;

namespace Services
{
    public static class APIService
    {
        private const int RETRY_COUNT = 2;

        // Path templates on their own: the host is prepended after formatting, so a host is never
        // read for placeholders.
        private const string CONTENT_PATH = "/content/contents/{0}";
        private const string PROFILE_PATH = "/lambdas/profiles/{0}";
        private const string ACTIVE_ENTITIES_PATH = "/content/entities/active";
        private const string MARKETPLACE_ITEM_PATH = "/v1/items?contractAddress={0}&itemId={1}";
        private const string MARKETPLACE_NFT_PATH = "/v1/nfts?contractAddress={0}&tokenId={1}";

        private static string EndpointMarketplace => MarketplaceServerUrl ?? $"https://marketplace-api.decentraland.{Environment}";

        /// <summary>
        /// The Catalyst every content request goes to: <see cref="PeerUrl"/>, or the one
        /// <see cref="Environment"/> picks.
        /// </summary>
        public static string PeerHost => PeerUrl ?? $"https://peer.decentraland.{Environment}";

        /// <summary>
        /// A Catalyst and a Marketplace API host the page chose; null keeps the one
        /// <see cref="Environment"/> picks.
        /// </summary>
        public static string PeerUrl { get; set; }

        public static string MarketplaceServerUrl { get; set; }

        /// <summary>
        /// The URL of a file on the Catalyst, by content hash.
        /// </summary>
        public static string ContentUrl(string hash) => PeerHost + string.Format(CONTENT_PATH, hash);

        public static string Environment { get; set; } = "org";

        public static async Awaitable<ProfileResponse.Avatar.AvatarData> GetAvatar(string profileID)
        {
            var profile = await GetWithRetry<ProfileResponse>(PeerHost, PROFILE_PATH, profileID);

            var avatar = profile.avatars[0].avatar;

            // Fix colors since they come without alpha
            avatar.eyes.color.a = 1f;
            avatar.hair.color.a = 1f;
            avatar.skin.color.a = 1f;

            // Convert dcl://base-avatars/ format to proper URN format
            for (var i = 0; i < avatar.wearables.Length; i++)
            {
                var urn = avatar.wearables[i];
                if (urn.StartsWith("dcl://base-avatars/"))
                {
                    urn = "urn:decentraland:off-chain:base-avatars:" +
                          avatar.wearables[i].Substring("dcl://base-avatars/".Length).ToLowerInvariant();
                }
                else
                {
                    urn = URNUtils.SanitizeURN(urn);
                }

                avatar.wearables[i] = urn;
            }

            return avatar;
        }

        public static Awaitable<MarketplaceItemResponse> GetMarketplaceItemFromID(string contract, string itemID) =>
            GetWithRetry<MarketplaceItemResponse>(EndpointMarketplace, MARKETPLACE_ITEM_PATH, contract, itemID);


        public static Awaitable<MarketplaceNTFResponse> GetMarketplaceItemFromToken(string contract, string tokenID) =>
            GetWithRetry<MarketplaceNTFResponse>(EndpointMarketplace, MARKETPLACE_NFT_PATH, contract, tokenID);

        public static async Awaitable<ActiveEntity[]> GetActiveEntities(string[] pointers)
        {
            var body = JsonUtility.ToJson(new ActiveEntitiesRequest(pointers));
            var retries = 0;

            // Each attempt owns its request, so the handlers of a failed one are released.
            while (true)
            {
                using var attempt = UnityWebRequest.Post(PeerHost + ACTIVE_ENTITIES_PATH, body, "application/json");
                await attempt.SendWebRequest();
                retries++;

                if (attempt.result == UnityWebRequest.Result.Success)
                {
                    // Newtonsoft (not JsonUtility) because ActiveEntity.springBones contains a
                    // Dictionary<string, Dictionary<string, SpringBoneParamsDto>> that JsonUtility can't bind.
                    return JsonConvert.DeserializeObject<ActiveEntity[]>(attempt.downloadHandler.text)
                           ?? Array.Empty<ActiveEntity>();
                }

                if (retries > RETRY_COUNT) throw new Exception(attempt.error);
            }
        }


        /// <summary>
        /// The body of <paramref name="url"/> as text, for a definition the page hosts itself.
        /// </summary>
        public static Awaitable<string> GetText(string url) => GetTextWithRetry(url);

        private static async Awaitable<T> GetWithRetry<T>(string host, string pathTemplate, params object[] args) =>
            JsonUtility.FromJson<T>(await GetTextWithRetry(host + string.Format(pathTemplate, args)));

        private static async Awaitable<string> GetTextWithRetry(string url)
        {
            var retries = 0;

            // Each attempt owns its request, so the handlers of a failed one are released.
            while (true)
            {
                using var attempt = UnityWebRequest.Get(url);
                await attempt.SendWebRequest();
                retries++;

                if (attempt.result == UnityWebRequest.Result.Success) return attempt.downloadHandler.text;
                if (retries > RETRY_COUNT) throw new Exception($"{attempt.error} ({url})");
            }
        }

    }
}