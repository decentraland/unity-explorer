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
            using PooledObject<HashSet<URN>> pooledHashSet = HashSetPool<URN>.Get(out var uniqueWearables);
            uniqueWearables = uniqueWearables.EnsureNotNull();
            uniqueWearables.Clear();
            equippedWearables.ToFullWearableUrns(wearableStorage, profile, uniqueWearables);

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
                            {
                                uniqueUrn = urn;
                                break;
                            }
                    }

                    // 11-21-2025
                    // HOTFIX for #5992
                    // Backend won't correctly equip the emote if we send the actual off-chain URN
                    // It will only work if we send the item ID, which is at the end of the string, after the last colon character
                    string urnString = uniqueUrn.ToString();
                    int separatorIndex = urnString.LastIndexOf(':');
                    if (separatorIndex >= 0) uniqueUrn = urnString[(separatorIndex + 1)..];

                    uniqueEmotes[i] = uniqueUrn;
                }
            }
        }

        public static void ToFullWearableUrns(this IEquippedWearables equippedWearables, IWearableStorage wearableStorage, Profile profile, ICollection<URN> collection)
        {
            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null || category == WearableCategories.Categories.BODY_SHAPE) continue;

                var potentialItemUrn = w.GetUrn();

                if (wearableStorage.TryGetOwnedNftRegistry(potentialItemUrn, out var registry) && registry.Count > 0)
                    potentialItemUrn = registry.Values.First().Urn;
                else if (profile?.Avatar?.Wearables != null)
                {
                    foreach (var profileWearable in profile.Avatar.Wearables)
                    {
                        if (profileWearable.Shorten() == potentialItemUrn)
                        {
                            potentialItemUrn = profileWearable;
                            break;
                        }
                    }
                }

                collection.Add(potentialItemUrn);
            }
        }

        /// <summary>
        ///     Populates a given collection with the short "Asset URNs" (as strings) for all equipped wearables.
        /// </summary>
        public static void ToShortWearableUrns(this IEquippedWearables equippedWearables, ICollection<string> collection)
        {
            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null || category == WearableCategories.Categories.BODY_SHAPE) continue;
                collection.Add(w.GetUrn().Shorten().ToString());
            }
        }

        /// <summary>
        ///     Returns a new list of full "Item URNs" (as strings) for all equipped wearables.
        ///     This is a convenience method that allocates a new list.
        /// </summary>
        public static List<string> ToFullWearableUrns(this IEquippedWearables equippedWearables, IWearableStorage wearableStorage, Profile profile)
        {
            // Use a temporary pooled list internally for efficiency
            using PooledObject<List<URN>> pooledUrns = ListPool<URN>.Get(out var urnList);
            urnList.Clear();
            equippedWearables.ToFullWearableUrns(wearableStorage, profile, urnList);

            // Create the final list for the caller
            var result = new List<string>(urnList.Count);
            foreach (var urn in urnList)
                result.Add(urn.ToString());

            return result;
        }

        /// <summary>
        ///     Returns a new list of short "Asset URNs" (as strings) for all equipped wearables.
        ///     This is a convenience method that allocates a new list.
        /// </summary>
        public static List<string> ToShortWearableUrns(this IEquippedWearables equippedWearables)
        {
            var result = new List<string>();
            equippedWearables.ToShortWearableUrns(result); // Calls the performant method
            return result;
        }
    }
}
