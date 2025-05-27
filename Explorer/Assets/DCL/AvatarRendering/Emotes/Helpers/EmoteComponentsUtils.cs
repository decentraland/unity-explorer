﻿using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmoteComponentsUtils
    {
        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<string> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get()!;

            foreach (URN emote in emotes)
                if (!emote.IsNullOrEmpty())
                    pointers.Add(emote);

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }

        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> emotes)
        {
            List<URN> pointers = POINTERS_POOL.Get()!;

            foreach (URN emote in emotes)
                if (!emote.IsNullOrEmpty())
                    pointers.Add(emote);

            // TODO: Remove timeout override
            return new GetEmotesByPointersIntention(pointers, bodyShape, timeout: 10);
        }
    }
}
