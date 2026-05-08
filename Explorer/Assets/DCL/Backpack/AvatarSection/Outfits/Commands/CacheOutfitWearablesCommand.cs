using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using UnityEngine.Pool;

namespace DCL.Backpack.AvatarSection.Outfits.Commands
{
    public class CacheOutfitWearablesCommand
    {
        private readonly IWearablesProvider wearablesProvider;
        private readonly IWearableStorage wearableStorage;

        public CacheOutfitWearablesCommand(IWearablesProvider wearablesProvider,
            IWearableStorage wearableStorage)
        {
            this.wearablesProvider = wearablesProvider;
            this.wearableStorage = wearableStorage;
        }

        public async UniTask ExecuteAsync(IReadOnlyCollection<URN>? wearableUrns, BodyShape bodyShape, CancellationToken ct, List<IWearable> result, bool useFullUrns)
        {
            if (wearableUrns == null || wearableUrns.Count == 0)
                return;

            using var a = HashSetPool<URN>.Get(out var baseUrns);
            using var b = ListPool<(URN baseUrn, URN fullUrn, string tokenId)>.Get(out var tokenMappings);
            using var c = ListPool<URN>.Get(out var missingUrns);

            foreach (var fullUrn in wearableUrns)
                if (!useFullUrns && TrySplitBaseAndToken(fullUrn, out var baseUrn, out string tokenId))
                {
                    baseUrns.Add(baseUrn);
                    tokenMappings.Add((baseUrn, fullUrn, tokenId));
                }
                else
                    // Off-chain or no token: just ensure DTO exists
                    baseUrns.Add(fullUrn);

            TryAdd(bodyShape, result, missingUrns);
            foreach (var w in baseUrns)
                TryAdd(w, result, missingUrns);

            try
            {
                if (missingUrns.Count > 0)
                    await wearablesProvider.GetByPointersAsync(missingUrns, bodyShape, ct, result);

                foreach ((var baseUrn, var fullUrn, string tokenId) in tokenMappings)
                    // We don't strictly need transferredAt/price here; use safe defaults.
                    if (wearableStorage.GetOwnedNftCount(baseUrn) == 0)
                        wearableStorage.SetOwnedNft(
                            baseUrn,
                            new NftBlockchainOperationEntry(
                                fullUrn,
                                tokenId,
                                DateTime.MinValue, // do we need this?
                                price: 0m // do we need this?
                            )
                        );

                ReportHub.Log(ReportCategory.OUTFITS,
                    $"[OUTFIT_PREWARM] Cached {baseUrns.Count} base DTOs and {tokenMappings.Count} token ownership entries.");
            }
            catch (OperationCanceledException)
            {
                /* expected */
            }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.OUTFITS); }
        }

        private void TryAdd(URN urn, List<IWearable> result, List<URN> missingUrns)
        {
            if (string.IsNullOrEmpty(urn)) return;

            if (wearableStorage.TryGetElement(urn, out IWearable w))
                result.Add(w);
            else
                missingUrns.Add(new URN(urn));
        }

        // Accepts V2 on-chain URNs of form:
        // urn:decentraland:matic:collections-v2:<collection>:<itemId>:<tokenId>
        // Returns baseUrn without the trailing tokenId and the tokenId itself.
        private static bool TrySplitBaseAndToken(URN fullUrn, out URN baseUrn, out string tokenId)
        {
            baseUrn = default;
            tokenId = string.Empty;

            // Ultra-defensive: split by ':' and detect a numeric-ish tail token
            string[]? parts = fullUrn.ToString().Split(':');
            if (parts.Length >= 7 && parts[0] == "urn" && parts[2] == "matic" && parts[3] == "collections-v2")
            {
                // last is token, rebuild base without last
                tokenId = parts[^1];
                string? baseStr = string.Join(':', parts, 0, parts.Length - 1);
                baseUrn = new URN(baseStr);
                return true;
            }

            return false;
        }
    }
}
