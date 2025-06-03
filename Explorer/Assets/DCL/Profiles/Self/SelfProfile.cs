using Arch.Core;
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
        private readonly IProfileCache profileCache;
        private readonly World world;
        private readonly Entity playerEntity;
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
            IReadOnlyList<URN>? forcedEmotes,
            IProfileCache profileCache,
            World world,
            Entity playerEntity)
        {
            this.profileRepository = profileRepository;
            this.web3IdentityCache = web3IdentityCache;
            this.equippedWearables = equippedWearables;
            this.wearableStorage = wearableStorage;
            this.emoteStorage = emoteStorage;
            this.equippedEmotes = equippedEmotes;
            this.forceRender = forceRender;
            this.forcedEmotes = forcedEmotes;
            this.profileCache = profileCache;
            this.world = world;
            this.playerEntity = playerEntity;
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

        /// <summary>Updates the profile based on the IEquippedEmotes, IEquippedWearables & force render</summary>
        /// <param name="ct"></param>
        /// <param name="updateAvatarInWorld">Updates the avatar in-world immediately and performs a revert operation in case of failure</param>
        /// <returns>The updated avatar</returns>
        public async UniTask<Profile?> UpdateProfileAsync(CancellationToken ct, bool updateAvatarInWorld = true)
        {
            Profile? profile = await ProfileAsync(ct);

            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            if (profile == null)
                throw new Exception("Self profile not found");

            Profile newProfile = profile.CreateNewProfileForUpdate(equippedEmotes, equippedWearables, forceRender, emoteStorage, wearableStorage,
                // Don't update the version as it will be incremented at UpdateProfileAsync function
                incrementVersion: false);

            return await UpdateProfileAsync(newProfile, ct, updateAvatarInWorld);
        }

        public async UniTask<Profile?> UpdateProfileAsync(Profile newProfile, CancellationToken ct, bool updateAvatarInWorld = true)
        {
            Profile? profile = await ProfileAsync(ct);

            if (web3IdentityCache.Identity == null)
                throw new Web3IdentityMissingException("Web3 Identity is not initialized");

            if (profile == null)
                throw new Exception("Self profile not found");

            // Skip publishing the same profile
            if (newProfile.IsSameProfile(profile)) return profile;

            newProfile.UserId = web3IdentityCache.Identity.Address;
            newProfile.Version++;
            newProfile.UserNameColor = ProfileNameColorHelper.GetNameColor(profile.DisplayName);

            OwnProfile = newProfile;

            if (!updateAvatarInWorld)
            {
                await profileRepository.SetAsync(newProfile, ct);
                return await profileRepository.GetAsync(newProfile.UserId, newProfile.Version, ct);
            }

            // Clone the old profile since the original will be disposed when its replaced in the cache
            var oldProfile = profileBuilder.From(profile).Build();

            // Update profile immediately to prevent UI inconsistencies
            // Without this immediate update, temporary desync can occur between backpack closure and catalyst validation
            // Example: Opening the emote wheel before catalyst validation would show outdated emote selections
            profileCache.Set(newProfile.UserId, newProfile);
            UpdateAvatarInWorld(newProfile);

            try
            {
                await profileRepository.SetAsync(newProfile, ct);
                Profile? savedProfile = await profileRepository.GetAsync(newProfile.UserId, newProfile.Version, ct);

                // We need to re-update the avatar in-world with the new profile because the save operation invalidates the previous profile
                // breaking the avatar and the backpack
                UpdateAvatarInWorld(savedProfile!);
                oldProfile.Dispose();
                return savedProfile;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Revert to the old profile so we are aligned to the catalyst's version
                profileCache.Set(oldProfile.UserId, oldProfile);
                UpdateAvatarInWorld(oldProfile);
                OwnProfile = oldProfile;
                throw;
            }
        }

        private void UpdateAvatarInWorld(Profile profile)
        {
            profile.IsDirty = true;

            bool found = world.Has<Profile>(playerEntity);

            if (found)
                world.Set(playerEntity, profile);
            else
                world.Add(playerEntity, profile);
        }
    }
}
