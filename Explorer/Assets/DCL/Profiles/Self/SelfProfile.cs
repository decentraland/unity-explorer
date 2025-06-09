using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Profiles.Helpers;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles.Self
{
    public class SelfProfile : ISelfProfile
    {
        private readonly IProfileRepository profileRepository;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableStorage wearableStorage;
        private readonly IEmoteStorage emoteStorage;
        private readonly IReadOnlyList<URN>? forcedEmotes;
        private readonly ProfileBuilder profileBuilder = new ();
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IReadOnlyList<string> forceRender;

        public SelfProfile(
            IProfileRepository profileRepository,
            IWeb3IdentityCache web3IdentityCache,
            IEquippedWearables equippedWearables,
            IWearableStorage wearableStorage,
            IEmoteStorage emoteStorage,
            IEquippedEmotes equippedEmotes,
            IReadOnlyList<string> forceRender,
            IReadOnlyList<URN>? forcedEmotes)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.equippedWearables = equippedWearables;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.equippedEmotes = equippedEmotes;
            this.forceRender = forceRender;
            this.forcedEmotes = forcedEmotes;
        }

        public Profile? OwnProfile { get; private set; }

        public async UniTask<Profile?> ProfileAsync(CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            Profile? profile = await profileRepository.GetAsync(
                web3IdentityCache.Identity.Address,
                ct
            );

            if (profile == null) return null;

            if (forcedEmotes != null)
                for (var slot = 0; slot < forcedEmotes.Count && slot < profile.Avatar.Emotes.Count; slot++)
                    profile.Avatar.emotes[slot] = forcedEmotes[slot];

            if (profile.Avatar.IsEmotesWheelEmpty())
                for (var slot = 0; slot < emoteStorage.EmbededURNs.Count && slot < profile.Avatar.Emotes.Count; slot++)
                    profile.Avatar.emotes[slot] = emoteStorage.EmbededURNs[slot];

            if (OwnProfile == null || profile.Version > OwnProfile.Version)
                OwnProfile = profile;

            return profile;
        }

        public async UniTask<Profile?> UpdateProfileAsync(CancellationToken ct)
        {
            Profile? profile = await ProfileAsync(ct);

            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            if (profile == null)
                throw new Exception("Self profile not found");

            Profile newProfile = profile.CreateNewProfileForUpdate(equippedEmotes, equippedWearables, forceRender, emoteStorage, wearableStorage);

            newProfile.UserId = web3IdentityCache.Identity.Address;

            // Skip publishing the same profile
            if (newProfile.Avatar.IsSameAvatar(profile.Avatar))
                return profile;

            newProfile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);
            OwnProfile = newProfile;

            await profileRepository.SetAsync(newProfile, ct);
            return await profileRepository.GetAsync(newProfile.UserId, newProfile.Version, ct);
        }

        public async UniTask<Profile?> UpdateProfileAsync(Profile profile, CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            Profile newProfile = profileBuilder.From(profile)
                                               .WithVersion(profile.Version + 1)
                                               .Build();

            newProfile.UserId = web3IdentityCache.Identity.Address;
            newProfile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);
            OwnProfile = newProfile;

            await profileRepository.SetAsync(newProfile, ct);
            return await profileRepository.GetAsync(newProfile.UserId, newProfile.Version, ct);
        }

        public async UniTask<Profile?> ForcePublishWithoutModificationsAsync(CancellationToken ct)
        {
            Profile? profile = await ProfileAsync(ct);

            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            if (profile == null)
                throw new Exception("Self profile not found");

            Profile newProfile = profileBuilder.From(profile)
                                               .WithVersion(profile.Version + 1)
                                               .Build();

            newProfile.UserId = web3IdentityCache.Identity.Address;
            newProfile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);
            OwnProfile = newProfile;

            await profileRepository.SetAsync(newProfile, ct);
            return await profileRepository.GetAsync(newProfile.UserId, newProfile.Version, ct);
        }
    }
}
