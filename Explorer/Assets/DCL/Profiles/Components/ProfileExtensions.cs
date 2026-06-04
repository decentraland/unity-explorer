using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Loading;
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
            IOwnedNftFilter? ownedNftFilter = null,
            bool incrementVersion = true)
        {
            using PooledObject<HashSet<URN>> pooledHashSet = HashSetPool<URN>.Get(out var uniqueWearables);
            uniqueWearables = uniqueWearables.EnsureNotNull();
            uniqueWearables.Clear();
            equippedWearables.ToFullWearableUrns(wearableStorage, profile, uniqueWearables, ownedNftFilter);

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
                    {
                        URN? pickedRegistryUrn = PickRegistryUrn(registry, ownedNftFilter);
                        if (pickedRegistryUrn.HasValue)
                            uniqueUrn = pickedRegistryUrn.Value;
                    }
                    else
                    {
                        foreach (URN urn in profile?.Avatar?.Emotes ?? Array.Empty<URN>())
                            if (urn.Shorten() == uniqueUrn && (ownedNftFilter == null || !ownedNftFilter.ShouldExclude(urn)))
                                uniqueUrn = urn;
                    }

                    uniqueEmotes[i] = uniqueUrn;
                }
            }
        }

        public static void ToFullWearableUrns(this IEquippedWearables equippedWearables, IWearableStorage wearableStorage, Profile profile, ICollection<URN> collection, IOwnedNftFilter? ownedNftFilter = null)
        {
            foreach ((string category, var w) in equippedWearables.Items())
            {
                if (w == null || category == WearableCategories.Categories.BODY_SHAPE) continue;

                var potentialItemUrn = w.GetUrn();

                if (wearableStorage.TryGetOwnedNftRegistry(potentialItemUrn, out var registry) && registry.Count > 0)
                {
                    URN? picked = PickRegistryUrn(registry, ownedNftFilter);
                    if (picked.HasValue)
                        potentialItemUrn = picked.Value;
                    // Every entry excluded: fall through with the base URN so the catalyst returns a coherent
                    // "not owned" instead of deploying a known-bad tokenId.
                }
                else if (profile?.Avatar?.Wearables != null)
                {
                    foreach (var profileWearable in profile.Avatar.Wearables)
                    {
                        if (profileWearable.Shorten() == potentialItemUrn && (ownedNftFilter == null || !ownedNftFilter.ShouldExclude(profileWearable)))
                        {
                            potentialItemUrn = profileWearable;
                            break;
                        }
                    }
                }

                collection.Add(potentialItemUrn);
            }
        }

        private static URN? PickRegistryUrn(IReadOnlyDictionary<URN, NftBlockchainOperationEntry> registry, IOwnedNftFilter? ownedNftFilter)
        {
            if (ownedNftFilter == null)
                return registry.Values.First().Urn;

            foreach (NftBlockchainOperationEntry entry in registry.Values)
                if (!ownedNftFilter.ShouldExclude(entry.Urn))
                    return entry.Urn;

            return null;
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
        public static List<string> ToFullWearableUrns(this IEquippedWearables equippedWearables, IWearableStorage wearableStorage, Profile profile, IOwnedNftFilter? ownedNftFilter = null)
        {
            // Use a temporary pooled list internally for efficiency
            using PooledObject<List<URN>> pooledUrns = ListPool<URN>.Get(out var urnList);
            urnList.Clear();
            equippedWearables.ToFullWearableUrns(wearableStorage, profile, urnList, ownedNftFilter);

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
