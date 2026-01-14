using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmoteComponentsUtils
    {
        /// Maps the legacy URN to the related new on-chain URN, i.e. :
        /// "wave" -> "urn:decentraland:off-chain:base-emotes:wave"
        private static readonly Dictionary<string, URN> LEGACY_TO_ON_CHAIN_EMOTE_URN_MAP = new (StringComparer.OrdinalIgnoreCase);

        public static void InitializeLegacyToOnChainEmoteMapping(IReadOnlyCollection<URN> embeddedEmoteUrns)
        {
            LEGACY_TO_ON_CHAIN_EMOTE_URN_MAP.Clear();

            foreach (URN embeddedUrn in embeddedEmoteUrns)
            {
                if (embeddedUrn.IsNullOrEmpty())
                    continue;

                string urnString = embeddedUrn.ToString();
                int lastColonIndex = urnString.LastIndexOf(':');

                if (lastColonIndex < 0 || lastColonIndex >= urnString.Length - 1) continue;

                string lastSegment = urnString.Substring(lastColonIndex + 1);
                LEGACY_TO_ON_CHAIN_EMOTE_URN_MAP[lastSegment] = embeddedUrn;
            }
        }

        /// <summary>
        /// Converts a legacy emote URN to its on-chain equivalent if a mapping exists.
        /// Returns the original URN if no conversion is needed.
        /// </summary>
        public static URN ConvertLegacyEmoteUrnToOnChain(URN emoteUrn)
        {
            if (emoteUrn.IsNullOrEmpty())
                return emoteUrn;

            string emoteId = emoteUrn.ToString();

            return LEGACY_TO_ON_CHAIN_EMOTE_URN_MAP.GetValueOrDefault(emoteId, emoteUrn);
        }

        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get();

            foreach (URN emote in emotes)
            {
                if (!emote.IsNullOrEmpty())
                    pointers.Add(ConvertLegacyEmoteUrnToOnChain(emote));
            }

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }
    }
}
