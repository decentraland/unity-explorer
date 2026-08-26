using System;
using UnityEngine;
using UnityEngine.Networking;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// One MANA→USD spot rate, for the toolbar's Stress Mode readout.
    ///
    /// **The source is a deliberate stand-in.** The rate was specified as "the MANA/USD oracle
    /// (<c>readManaUsdRate</c> in <c>mana-rate.ts</c>)", but that file is in another Decentraland repo —
    /// this one contains no TypeScript at all — so the oracle's chain, contract and decoding weren't
    /// available to port. Rather than guess a contract address (a wrong one fails silently or, worse,
    /// reads a plausible number off the wrong feed), this reads CoinGecko's public price endpoint, which
    /// needs no key and no chain access. <see cref="Fetch"/> is the entire seam: swap its request and
    /// parse for an <c>eth_call</c> to the aggregator and nothing else in the tool changes.
    ///
    /// Callback-based rather than Awaitable for the same reason <see cref="CatalogService"/> is: the
    /// window runs in the editor without play mode, where Awaitable's player loop isn't ticking.
    /// </summary>
    public static class ManaRateService
    {
        /// <summary>
        /// CoinGecko's id for MANA is <c>decentraland</c> (the token's id, not the DAO's). No key needed
        /// on this endpoint, which is the reason it's the stand-in — nothing to store or leak.
        /// </summary>
        private const string ENDPOINT =
            "https://api.coingecko.com/api/v3/simple/price?ids=decentraland&vs_currencies=usd";

        /// <summary>
        /// Fetches USD per MANA. Exactly one of the callbacks fires, always on the main thread (the
        /// request's completion event), so a UI label can be written straight from them.
        /// </summary>
        public static void Fetch(Action<float> onSuccess, Action<string> onError)
        {
            var request = UnityWebRequest.Get(ENDPOINT);
            var operation = request.SendWebRequest();

            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onError?.Invoke(request.error);
                        return;
                    }

                    var response = JsonUtility.FromJson<Response>(request.downloadHandler.text);
                    var usd = response?.decentraland?.usd ?? 0f;

                    // A zero (or a rate-limit body that parsed into an empty object) is not a price.
                    // Reporting it as one would put "$0.0000" in the toolbar, which reads as a crash.
                    if (usd <= 0f)
                    {
                        onError?.Invoke("no rate in response");
                        return;
                    }

                    onSuccess?.Invoke(usd);
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

        // JsonUtility maps by field name, so these mirror the response's shape:
        // {"decentraland":{"usd":0.2814}}
        [Serializable]
        private class Response
        {
            public Entry decentraland;
        }

        [Serializable]
        private class Entry
        {
            public float usd;
        }
    }
}
