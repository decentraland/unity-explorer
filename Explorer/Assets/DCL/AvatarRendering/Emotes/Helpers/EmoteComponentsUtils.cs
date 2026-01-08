using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using System;
using System.Collections.Generic;
using static DCL.AvatarRendering.Wearables.Helpers.WearableComponentsUtils;

namespace DCL.AvatarRendering.Emotes
{
    public static class EmoteComponentsUtils
    {
        public static GetEmotesByPointersIntention CreateGetEmotesByPointersIntention(BodyShape bodyShape, IReadOnlyCollection<URN> emotes)
        {
            UnityEngine.Debug.Log($"JUANI WHERE THIS IS COMING FROM {Environment.StackTrace}");
            List<URN> pointers = POINTERS_POOL.Get()!;

            foreach (URN emote in emotes)
                if (!emote.IsNullOrEmpty())
                    pointers.Add(emote);

            return new GetEmotesByPointersIntention(pointers, bodyShape);
        }
    }
}
