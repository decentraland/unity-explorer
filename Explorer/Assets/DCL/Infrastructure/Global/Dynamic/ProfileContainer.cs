using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.Chat;
using DCL.Backpack.Gifting.Services;
using DCL.Backpack.Gifting.Services.PendingTransfers;
using DCL.Backpack.Gifting.Services.SnapshotEquipped;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Web3.Identities;
using Global.AppArgs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Global.Dynamic
{
    /// <summary>
    ///     Own-profile state: self profile, equipped wearables/emotes, profile thumbnails, profile change bus
    ///     and pending gift transfers the profile depends on.
    /// </summary>
    public class ProfileContainer : IDisposable
    {
        public EquippedWearables EquippedWearables { get; }

        public EquippedEmotes EquippedEmotes { get; }

        public PendingTransferService PendingTransferService { get; }

        public SelfProfile SelfProfile { get; }

        public IAvatarEquippedStatusProvider EquippedStatusProvider { get; }

        public ISpriteCache ThumbnailCache { get; }

        public ProfileRepositoryWrapper ProfileRepositoryWrapper { get; }

        public ProfileChangesBus ProfileChangesBus { get; }

        private ProfileContainer(
            EquippedWearables equippedWearables,
            EquippedEmotes equippedEmotes,
            PendingTransferService pendingTransferService,
            SelfProfile selfProfile,
            IAvatarEquippedStatusProvider equippedStatusProvider,
            ISpriteCache thumbnailCache,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ProfileChangesBus profileChangesBus)
        {
            EquippedWearables = equippedWearables;
            EquippedEmotes = equippedEmotes;
            PendingTransferService = pendingTransferService;
            SelfProfile = selfProfile;
            EquippedStatusProvider = equippedStatusProvider;
            ThumbnailCache = thumbnailCache;
            ProfileRepositoryWrapper = profileRepositoryWrapper;
            ProfileChangesBus = profileChangesBus;
        }

        public static ProfileContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IWeb3IdentityCache identityCache,
            World globalWorld,
            Entity playerEntity,
            WearableContainer wearableContainer)
        {
            var equippedWearables = new EquippedWearables();
            var equippedEmotes = new EquippedEmotes();

            var selfEmotes = new List<URN>();
            ParseParamsForcedEmotes(bootstrapContainer.AppArgs, ref selfEmotes);
            ParseDebugForcedEmotes(bootstrapContainer.DebugSettings.EmotesToAddToUserProfile, ref selfEmotes);

            IProfileRepository profilesRepository = staticContainer.ProfilesContainer.Repository;
            IProfileCache profileCache = staticContainer.ProfilesContainer.Cache;

            IGiftingPersistence giftingPersistence = new PlayerPrefsGiftingPersistence(identityCache);
            var pendingTransferService = new PendingTransferService(giftingPersistence, identityCache, wearableContainer.WearableCatalog, staticContainer.EmoteStorage);

            var selfProfile = new SelfProfile(profilesRepository, identityCache, equippedWearables, wearableContainer.WearableCatalog,
                staticContainer.EmoteStorage, equippedEmotes, selfEmotes, profileCache, globalWorld, playerEntity,
                pendingTransferService);

            ISpriteCache thumbnailCache = new SpriteCache(staticContainer.WebRequestsContainer.WebRequestController);
            var profileRepositoryWrapper = new ProfileRepositoryWrapper(profilesRepository, profileCache, thumbnailCache, identityCache);
            GetProfileThumbnailCommand.Initialize(new GetProfileThumbnailCommand(profileRepositoryWrapper));

            return new ProfileContainer(
                equippedWearables,
                equippedEmotes,
                pendingTransferService,
                selfProfile,
                new AvatarEquippedStatusProvider(selfProfile),
                thumbnailCache,
                profileRepositoryWrapper,
                new ProfileChangesBus());
        }

        public ProfilePlugin CreateProfilePlugin(StaticContainer staticContainer) =>
            new (staticContainer.ProfilesContainer.Repository, staticContainer.ProfilesContainer.Cache, staticContainer.CacheCleaner);

        public GiftingPlugin CreateGiftingPlugin(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IAssetsProvisioner assetsProvisioner,
            UIShellContainer uiShellContainer,
            WearableContainer wearableContainer,
            ChatEventBus chatEventBus,
            IWeb3IdentityCache identityCache) =>
            new (assetsProvisioner,
                uiShellContainer.MvcManager,
                PendingTransferService,
                staticContainer.WebRequestsContainer.WebRequestController,
                EquippedStatusProvider,
                staticContainer.ProfilesContainer.Repository,
                staticContainer.InputBlock,
                wearableContainer.WearablesProvider,
                wearableContainer.WearableCatalog,
                staticContainer.EmoteStorage,
                wearableContainer.EmoteProvider,
                identityCache,
                wearableContainer.ThumbnailProvider,
                chatEventBus,
                bootstrapContainer.WebBrowser,
                bootstrapContainer.CompositeWeb3Provider,
                bootstrapContainer.DecentralandUrlsSource,
                staticContainer.ImageControllerProvider);

        public void Dispose()
        {
            SelfProfile.Dispose();
            ProfileRepositoryWrapper.Dispose();
            PendingTransferService.Dispose();
        }

        private static void ParseDebugForcedEmotes(IReadOnlyCollection<string>? debugEmotes, ref List<URN> parsedEmotes)
        {
            if (debugEmotes?.Count > 0)
                parsedEmotes.AddRange(debugEmotes.Select(emote => new URN(emote)));
        }

        private static void ParseParamsForcedEmotes(IAppArgs appParams, ref List<URN> parsedEmotes)
        {
            if (appParams.TryGetValue(AppArgsFlags.FORCED_EMOTES, out string? csv) && !string.IsNullOrEmpty(csv!))
                parsedEmotes.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries)?.Select(emote => new URN(emote)) ?? ArraySegment<URN>.Empty);
        }
    }
}
