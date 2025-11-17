using System.Collections.Generic;
using CommunicationData.URLHelpers;
using DCL.Profiles;

namespace DCL.Backpack.Gifting.Cache
{
    public class EquippedItemContext
    {
        // Stores the full URNs with token IDs, e.g., "...:0:3"
        private readonly HashSet<string> equippedFullUrns = new ();

        public void Populate(Avatar avatar)
        {
            equippedFullUrns.Clear();

            foreach (var wearableUrn in avatar.Wearables)
                if (!wearableUrn.IsNullOrEmpty())
                    equippedFullUrns.Add(wearableUrn.ToString());

            foreach (var emoteUrn in avatar.Emotes)
                if (!emoteUrn.IsNullOrEmpty())
                    equippedFullUrns.Add(emoteUrn.ToString());
        }

        /// <summary>
        ///     The simple solution for the grid presenter. Checks if any equipped URN
        ///     starts with the provided base URN.
        /// </summary>
        public bool IsItemTypeEquipped(URN baseUrn)
        {
            string baseUrnString = baseUrn.ToString();
            foreach (string fullUrn in equippedFullUrns)
            {
                // This handles both exact matches (for off-chain items) and
                // prefix matches (for on-chain items).
                if (fullUrn.StartsWith(baseUrnString))
                {
                    // Ensure it's a true match, not a partial one (e.g., ...:1 vs ...:10)
                    if (fullUrn.Length == baseUrnString.Length || fullUrn[baseUrnString.Length] == ':')
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Checks if a specific instance (with a token ID) is equipped.
        /// </summary>
        public bool IsSpecificInstanceEquipped(URN fullUrn)
        {
            return equippedFullUrns.Contains(fullUrn.ToString());
        }
    }
}