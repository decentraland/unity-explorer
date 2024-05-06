using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
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
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IEmoteCache emoteCache;
        private readonly IReadOnlyList<string> forceRender;
        private readonly ProfileBuilder profileBuilder = new ();

        public SelfProfile(
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IEquippedWearables equippedWearables,
            IWearableCatalog wearableCatalog,
            IEmoteCache emoteCache,
            IEquippedEmotes equippedEmotes,
            IReadOnlyList<string> forceRender
        )
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.equippedWearables = equippedWearables;
            this.wearableCatalog = wearableCatalog;
            this.emoteCache = emoteCache;
            this.equippedEmotes = equippedEmotes;
            this.forceRender = forceRender;
        }

        public UniTask<Profile?> ProfileAsync(CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            return profileRepository.GetAsync(
                web3IdentityCache.Identity.Address,
                ct
            );
        }

        public async UniTask<Profile?> PublishAsync(CancellationToken ct)
        {
            Profile? profile = await ProfileAsync(ct);

            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            if (profile == null)
            {
                profile = Profile.NewRandomProfile(web3IdentityCache.Identity.Address);
                await profileRepository.SetAsync(profile, ct);
                return await profileRepository.GetAsync(profile.UserId, profile.Version, ct);
            }

            using var _ = HashSetPool<URN>.Get(out HashSet<URN> uniqueWearables);

            uniqueWearables = uniqueWearables.EnsureNotNull();
            ConvertEquippedWearablesIntoUniqueUrns(profile, uniqueWearables);

            var uniqueEmotes = new URN[profile?.Avatar.Emotes.Count ?? 0];
            ConvertEquippedEmotesIntoUniqueUrns(profile, uniqueEmotes);

            var bodyShape = BodyShape.FromStringSafe(equippedWearables.Wearable(WearablesConstants.Categories.BODY_SHAPE)!.GetUrn());

            profile = profileBuilder.From(profile)
                                    .WithBodyShape(bodyShape)
                                    .WithWearables(uniqueWearables)
                                    .WithEmotes(uniqueEmotes)
                                    .WithForceRender(forceRender)
                                    .WithVersion(profile!.Version + 1)
                                    .Build();

            profile.UserId = web3IdentityCache.Identity.Address;

            await profileRepository.SetAsync(profile, ct);
            return await profileRepository.GetAsync(profile.UserId, profile.Version, ct);
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

        private void ConvertEquippedEmotesIntoUniqueUrns(Profile? profile, IList<URN> uniqueEmotes)
        {
            for (var i = 0; i < equippedEmotes.SlotCount; i++)
            {
                IEmote? w = equippedEmotes.EmoteInSlot(i);

                if (w == null) continue;

                URN uniqueUrn = w.GetUrn();

                if (!uniqueUrn.IsExtended())
                {
                    if (emoteCache.TryGetOwnedNftRegistry(uniqueUrn, out IReadOnlyDictionary<URN, NftBlockchainOperationEntry>? registry))
                        uniqueUrn = registry.First().Value.Urn;
                    else
                    {
                        foreach (URN urn in profile?.Avatar.Emotes ?? Array.Empty<URN>())
                            if (urn.Shorten() == uniqueUrn)
                                uniqueUrn = urn;
                    }
                }

                uniqueEmotes[i] = uniqueUrn;
            }
        }
    }
}
