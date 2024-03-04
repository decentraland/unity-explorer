using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.Web3.Identities;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using Utility;

namespace DCL.Backpack
{
    public class BackpackEquipStatusController : IBackpackEquipStatusController
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly Dictionary<string, IWearable?> equippedWearables = new ();
        private readonly ProfileBuilder profileBuilder = new ();

        private CancellationTokenSource? publishProfileCts;

        public BackpackEquipStatusController(IBackpackEventBus backpackEventBus,
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
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

                foreach ((string category, IWearable? w) in equippedWearables)
                {
                    if (w == null) continue;
                    if (category == WearablesConstants.Categories.BODY_SHAPE) continue;
                    var urn = new URN(w.WearableDTO.Asset.metadata.id);
                    uniqueWearables.Add(urn);
                }

                profile = profileBuilder.From(profile!)
                                        .WithWearables(uniqueWearables)
                                        .Build();

                HashSetPool<URN>.Release(uniqueWearables);

                await profileRepository.SetAsync(profile, ct);
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
    }

    public interface IBackpackEquipStatusController
    {
        IWearable? GetEquippedWearableForCategory(string category);

        bool IsWearableEquipped(IWearable wearable);
    }
}
