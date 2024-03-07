using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IBackpackEquipStatusController
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableCatalog wearableCatalog;
        private readonly Func<(World, Entity)> ecsContextProvider;
        private readonly Dictionary<string, IWearable?> equippedWearables = new ();
        private readonly ProfileBuilder profileBuilder = new ();

        private World? world;
        private Entity? playerEntity;
        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(IBackpackEventBus backpackEventBus,
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IWearableCatalog wearableCatalog,
            Func<(World, Entity)> ecsContextProvider)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.wearableCatalog = wearableCatalog;
            this.ecsContextProvider = ecsContextProvider;
            backpackEventBus.EquipEvent += SetWearableForCategory;
            backpackEventBus.UnEquipEvent += RemoveWearableForCategory;
            backpackEventBus.PublishProfileEvent += PublishProfile;

            foreach (string category in WearablesConstants.CATEGORIES_PRIORITY)
                equippedWearables.Add(category, null);
        }

        private void PublishProfile()
        {
            async UniTaskVoid PublishProfileAsync(CancellationToken ct)
            {
                Profile? profile = await profileRepository.GetAsync(web3IdentityCache.Identity!.Address, 0, CancellationToken.None);

                HashSet<URN> uniqueWearables = HashSetPool<URN>.Get();

                ConvertEquippedWearablesIntoUniqueUrns(profile!, uniqueWearables);

                profile = profileBuilder.From(profile!)
                                        .WithWearables(uniqueWearables)
                                        .Build();

                HashSetPool<URN>.Release(uniqueWearables);

                await profileRepository.SetAsync(profile, ct);

                // TODO: is it a single responsibility issue? perhaps we can move it elsewhere?
                UpdateAvatarInWorld(profile);
            }

            publishProfileCts = publishProfileCts.SafeRestart();
            PublishProfileAsync(publishProfileCts.Token).Forget();
        }

        public IWearable? GetEquippedWearableForCategory(string category) =>
            equippedWearables[category];

        public bool IsWearableEquipped(IWearable wearable) =>
            equippedWearables[wearable.GetCategory()] == wearable;

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

        bool IsWearableEquipped(IWearable wearable);
    }
}
