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
        // TODO MAURIZIO remove

        // private static string[] LEGACY_EMOTE_IDS = new []
        // {
        //     "wave",
        //     "fistpump",
        //     "dance",
        //     "raiseHand",
        //     "clap",
        //     "money",
        //     "kiss",
        //     "shrug",
        //     "headexplode",
        //     "cry",
        //     "dab",
        //     "disco",
        //     "dontsee",
        //     "hammer",
        //     "handsair",
        //     "hohoho",
        //     "robot",
        //     "snowfall",
        //     "tektonik",
        //     "tik",
        //     "confettipopper",
        // };

        private static readonly Dictionary<string, URN> EMBEDDED_EMOTE_URN_MAP = new (StringComparer.OrdinalIgnoreCase);

        public static void InitializeEmbeddedEmoteMapping(IReadOnlyCollection<URN> embeddedEmoteUrns)
        {
            EMBEDDED_EMOTE_URN_MAP.Clear();

            foreach (URN embeddedUrn in embeddedEmoteUrns)
            {
                if (embeddedUrn.IsNullOrEmpty())
                    continue;

                string urnString = embeddedUrn.ToString();
                int lastColonIndex = urnString.LastIndexOf(':');

                if (lastColonIndex < 0 || lastColonIndex >= urnString.Length - 1) continue;

                string lastSegment = urnString.Substring(lastColonIndex + 1);
                EMBEDDED_EMOTE_URN_MAP[lastSegment] = embeddedUrn;
            }
        }

        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get()!;

            foreach (URN emote in emotes)
            {
                if (emote.IsNullOrEmpty())
                    continue;

                string emoteId = emote.ToString();

                // If it's a legacy emote, try to find a matching embedded emote URN by last segment
                if (EMBEDDED_EMOTE_URN_MAP.TryGetValue(emoteId, out URN embeddedUrn))
                {
                    pointers.Add(embeddedUrn);
                    continue;
                }

                pointers.Add(emote);
            }

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }
    }
}
