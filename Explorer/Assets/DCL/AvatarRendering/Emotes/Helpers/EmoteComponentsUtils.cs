using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables;
using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmoteComponentsUtils
    {
        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get();

            foreach (URN emote in emotes)
            {
                if (!emote.IsNullOrEmpty())
                    pointers.Add(emote);
            }

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }

        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get();

            foreach (URN emote in emotes)
            {
                if (!emote.IsNullOrEmpty())
                    pointers.Add(emote);
            }

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }
    }
}
