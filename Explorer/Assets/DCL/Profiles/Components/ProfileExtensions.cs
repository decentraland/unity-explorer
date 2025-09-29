using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Utilities.Extensions;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    public static class ProfileExtensions
    {
        private static readonly ProfileBuilder PROFILE_BUILDER = new ();

        public static Profile CreateNewProfileForUpdate(this Profile profile,
            IEquippedEmotes equippedEmotes,
            IEquippedWearables equippedWearables,
            IReadOnlyList<string> forceRender,
            IEmoteStorage emoteStorage,
            IWearableStorage wearableStorage,
            bool incrementVersion = true)
        {
            using PooledObject<HashSet<URN>> _ = HashSetPool<URN>.Get(out HashSet<URN> uniqueWearables);

            uniqueWearables = uniqueWearables.EnsureNotNull();
            ConvertEquippedWearablesIntoUniqueUrns();

            var uniqueEmotes = new URN[profile.Avatar.Emotes.Count];
            ConvertEquippedEmotesIntoUniqueUrns();

            var bodyShape = BodyShape.FromStringSafe(equippedWearables.Wearable(WearableCategories.Categories.BODY_SHAPE)!.GetUrn());

            ProfileBuilder builder = PROFILE_BUILDER.From(profile)
                                                           .WithBodyShape(bodyShape)
                                                           .WithWearables(uniqueWearables)
                                                           .WithColors(equippedWearables.GetColors())
                                                           .WithEmotes(uniqueEmotes)
                                                           .WithForceRender(forceRender);

            if (incrementVersion)
                builder = builder.WithVersion(profile.Version + 1);

            Profile newProfile = builder.Build();

            return newProfile;

            void ConvertEquippedWearablesIntoUniqueUrns()
            {
                foreach ((string category, IWearable? w) in equippedWearables.Items())
                {
                    if (w == null) continue;
                    if (category == WearableCategories.Categories.BODY_SHAPE) continue;

                    URN uniqueUrn = w.GetUrn();

                    if (wearableStorage.TryGetOwnedNftRegistry(uniqueUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry))
                        uniqueUrn = registry.First().Value.Urn;
                    else
                    {
                        foreach (URN profileWearable in profile?.Avatar?.Wearables ?? Array.Empty<URN>())
                            if (profileWearable.Shorten() == uniqueUrn)
                                uniqueUrn = profileWearable;
                    }

                    uniqueWearables.Add(uniqueUrn);
                }
            }

            void ConvertEquippedEmotesIntoUniqueUrns()
            {
                for (var i = 0; i < equippedEmotes.SlotCount; i++)
                {
                    IEmote? w = equippedEmotes.EmoteInSlot(i);

                    if (w == null) continue;

                    URN uniqueUrn = w.GetUrn();

                    if (emoteStorage.TryGetOwnedNftRegistry(uniqueUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry))
                        uniqueUrn = registry.First().Value.Urn;
                    else
                    {
                        foreach (URN urn in profile?.Avatar?.Emotes ?? Array.Empty<URN>())
                            if (urn.Shorten() == uniqueUrn)
                                uniqueUrn = urn;
                    }

                    uniqueEmotes[i] = uniqueUrn;
                }
            }
        }
    }
}
