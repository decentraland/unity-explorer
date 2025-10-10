using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiles;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.Backpack.Outfits.Extensions
{
    public static class OutfitsExtensions
    {
        /// <summary>
        ///     Converts the list of currently equipped wearables into a list of full "Item URNs"
        ///     including the tokenId, as required by the Catalyst deployment process.
        ///     It uses the provided wearableStorage and profile as data sources for the conversion.
        /// </summary>
        public static List<string> ToFullWearableUrns(this IEquippedWearables equippedWearables, IWearableStorage wearableStorage, Profile profile)
        {
            var fullItemUrns = new List<string>();

            foreach ((string category, var w) in equippedWearables.Items())
            {
                // Skip empty slots and the body shape, which is handled separately
                if (w == null || category == WearableCategories.Categories.BODY_SHAPE) continue;

                // Start with the base "Asset URN" (e.g., ...:2)
                var potentialItemUrn = w.GetUrn();

                // ATTEMPT 1: Find the specific NFT the user owns for this wearable.
                // This is the most reliable way to get the full "Item URN" with the tokenId.
                if (wearableStorage.TryGetOwnedNftRegistry(potentialItemUrn,
                        out var registry) && registry.Count > 0)
                {
                    // Success! We found the specific NFT. Use its full URN.
                    potentialItemUrn = registry.First().Value.Urn;
                }
                // ATTEMPT 2 (Fallback): If it's not an NFT (e.g., a default wearable),
                // try to find the full URN from the original profile's wearable list.
                else
                {
                    foreach (var profileWearable in profile?.Avatar?.Wearables ?? Array.Empty<URN>())
                    {
                        if (profileWearable.Shorten() == potentialItemUrn)
                            potentialItemUrn = profileWearable;
                    }
                }

                fullItemUrns.Add(potentialItemUrn.ToString());
            }

            return fullItemUrns;
        }
    }
}