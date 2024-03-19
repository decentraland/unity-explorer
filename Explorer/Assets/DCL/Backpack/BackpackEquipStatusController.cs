using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IBackpackEquipStatusController
    {
        private static readonly ArrayPool<URN> OWNED_EMOTES_ARRAY_POOL = ArrayPool<URN>.Create(10, 10);

        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableCatalog wearableCatalog;
        private readonly IEmoteCache emoteCache;
        private readonly Func<(World, Entity)> ecsContextProvider;
        private readonly Dictionary<string, IWearable?> equippedWearables = new ();
        private readonly IEmote?[] equippedEmotes = new IEmote[10];
        private readonly ProfileBuilder profileBuilder = new ();

        private World? world;
        private Entity? playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(IBackpackEventBus backpackEventBus,
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IWearableCatalog wearableCatalog,
            IEmoteCache emoteCache,
            Func<(World, Entity)> ecsContextProvider)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.wearableCatalog = wearableCatalog;
            this.emoteCache = emoteCache;
            this.ecsContextProvider = ecsContextProvider;
            backpackEventBus.EquipWearableEvent += SetWearableForCategory;
            backpackEventBus.UnEquipWearableEvent += RemoveWearableForCategory;
            backpackEventBus.PublishProfileEvent += PublishProfile;
            backpackEventBus.EquipEmoteEvent += EquipEmote;
            backpackEventBus.UnEquipEmoteEvent += UnEquipEmote;

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
                equippedWearables.Add(category, null);
        }

        private void PublishProfile()
        {
            async UniTaskVoid PublishProfileAsync(CancellationToken ct)
            {
                Profile? profile = await profileRepository.GetAsync(web3IdentityCache.Identity!.Address, 0, CancellationToken.None);

                HashSet<URN> uniqueWearables = HashSetPool<URN>.Get();
                URN[] uniqueEmotes = OWNED_EMOTES_ARRAY_POOL.Rent(10);

                ConvertEquippedWearablesIntoUniqueUrns(profile!, uniqueWearables);
                ConvertEquippedEmotesIntoUniqueUrns(profile!, uniqueEmotes);

                profile = profileBuilder.From(profile!)
                                        .WithWearables(uniqueWearables)
                                        .WithEmotes(uniqueEmotes)
                                        .Build();

                HashSetPool<URN>.Release(uniqueWearables);
                OWNED_EMOTES_ARRAY_POOL.Return(uniqueEmotes);

                await profileRepository.SetAsync(profile, ct);

                // TODO: is it a single responsibility issue? perhaps we can move it elsewhere?
                UpdateAvatarInWorld(profile);
            }

            publishProfileCts = publishProfileCts.SafeRestart();
            PublishProfileAsync(publishProfileCts.Token).Forget();
        }

        public IWearable? GetEquippedWearableForCategory(string category) =>
            equippedWearables[category];

        public IEmote? GetEquippedEmote(int slot) =>
            equippedEmotes[slot];

        public bool IsWearableEquipped(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] == wearable;

        public bool IsEmoteEquipped(IEmote emote)
        {
            foreach (IEmote? equippedEmote in equippedEmotes)
            {
                if (equippedEmote == null) continue;
                if (equippedEmote == emote) return true;
            }

            return false;
        }

        //This will retrieve the list of default hides for the current equipped wearables
        //Manual hide override will be a separate task
        //TODO retrieve logic from old renderer
        public List<string> GetCurrentWearableHides()
        {
            List<string> hides = new List<string>();

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
            {
                IWearable? wearable = equippedWearables[category];

                if (wearable == null)
                    continue;
            }

            return hides;
        }

        private void RemoveWearableForCategory(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] = null;

        private void SetWearableForCategory(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] = wearable;

        private void EquipEmote(int slot, IEmote emote) =>
            equippedEmotes[slot] = emote;

        private void UnEquipEmote(int slot, IEmote? emote) =>
            equippedEmotes[slot] = null;

        private void ConvertEquippedEmotesIntoUniqueUrns(Profile profile, IList<URN> uniqueEmotes)
        {
            for (var i = 0; i < equippedEmotes.Length; i++)
            {
                IEmote? w = equippedEmotes[i];

                if (w == null) continue;

                URN uniqueUrn = w.GetUrn();

                if (!uniqueUrn.IsExtended())
                {
                    if (emoteCache.TryGetOwnedNftRegistry(uniqueUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry))
                        uniqueUrn = registry.First().Value.Urn;
                    else
                    {
                        foreach (URN urn in profile.Avatar.Emotes)
                            if (urn.Shorten() == uniqueUrn)
                                uniqueUrn = urn;
                    }
                }

                uniqueEmotes[i] = uniqueUrn;
            }
        }

        private void ConvertEquippedWearablesIntoUniqueUrns(Profile profile, HashSet<URN> uniqueWearables)
        {
            foreach ((string category, IWearable? w) in equippedWearables)
            {
                if (w == null) continue;
                if (category == WearablesConstants.Categories.BODY_SHAPE) continue;

                URN uniqueUrn = w.GetUrn();

                if (!uniqueUrn.IsExtended())
                {
                    if (wearableCatalog.TryGetOwnedNftRegistry(uniqueUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry))
                        uniqueUrn = registry.First().Value.Urn;
                    else
                    {
                        foreach (URN profileWearable in profile.Avatar.Wearables)
                            if (profileWearable.Shorten() == uniqueUrn)
                                uniqueUrn = profileWearable;
                    }
                }

                uniqueWearables.Add(uniqueUrn);
            }
        }

        private void UpdateAvatarInWorld(Profile profile)
        {
            if (world == null || !playerEntity.HasValue)
            {
                (World? w, Entity e) = ecsContextProvider.Invoke();
                world = w;
                playerEntity = e;
            }

            profile.IsDirty = true;

            bool found = world.Has<Profile>(playerEntity.Value);

            if (found)
                world.Set(playerEntity.Value, profile);
            else
                world.Add(playerEntity.Value, profile);
        }
    }

    public interface IBackpackEquipStatusController
    {
        IWearable? GetEquippedWearableForCategory(string category);

        IEmote? GetEquippedEmote(int slot);

        bool IsWearableEquipped(IWearable wearable);

        bool IsEmoteEquipped(IEmote emote);
    }
}
