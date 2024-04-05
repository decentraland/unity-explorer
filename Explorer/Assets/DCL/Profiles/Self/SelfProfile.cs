using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Profiles.Self
{
    public class SelfProfile : ISelfProfile
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWearableCatalog wearableCatalog;
        private readonly ProfileBuilder profileBuilder = new ();

        public SelfProfile(IProfileRepository profileRepository, IWeb3IdentityCache web3IdentityCache, IEquippedWearables equippedWearables, IWearableCatalog wearableCatalog)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.equippedWearables = equippedWearables;
            this.wearableCatalog = wearableCatalog;
        }

        public UniTask<Profile?> ProfileAsync(CancellationToken ct) =>
            profileRepository.GetAsync(web3IdentityCache.Identity!.Address, ct);

        public async UniTask PublishAsync(CancellationToken ct)
        {
            Profile? profile = await ProfileAsync(ct);

            using (var _ = HashSetPool<URN>.Get(out HashSet<URN> uniqueWearables))
            {
                uniqueWearables = uniqueWearables.EnsureNotNull();
                ConvertEquippedWearablesIntoUniqueUrns(profile, uniqueWearables);

                profile = profileBuilder.From(profile)
                                        .WithWearables(uniqueWearables)
                                        .Build();
            }

            await profileRepository.SetAsync(profile, ct);
        }

        private void ConvertEquippedWearablesIntoUniqueUrns(Profile? profile, ISet<URN> uniqueWearables)
        {
            foreach ((string category, IWearable? w) in equippedWearables.Items())
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
                        foreach (URN profileWearable in profile?.Avatar?.Wearables ?? Array.Empty<URN>())
                            if (profileWearable.Shorten() == uniqueUrn)
                                uniqueUrn = profileWearable;
                    }
                }

                uniqueWearables.Add(uniqueUrn);
            }
        }
    }
}
