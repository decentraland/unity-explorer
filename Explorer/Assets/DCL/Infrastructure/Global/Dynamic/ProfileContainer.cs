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
using DCL.DebugUtilities;
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
            ParseParamsUrns(bootstrapContainer.AppArgs, AppArgsFlags.FORCED_EMOTES, selfEmotes);
            ParseDebugUrns(bootstrapContainer.DebugSettings.EmotesToAddToUserProfile, selfEmotes);

            var selfWearables = new List<URN>();
            ParseParamsUrns(bootstrapContainer.AppArgs, AppArgsFlags.FORCED_WEARABLES, selfWearables);
            ParseDebugUrns(bootstrapContainer.DebugSettings.WearablesToAddToUserProfile, selfWearables);
            var forcedWearables = new ForcedWearables(selfWearables);

            IProfileRepository profilesRepository = staticContainer.ProfilesContainer.Repository;
            IProfileCache profileCache = staticContainer.ProfilesContainer.Cache;

            IGiftingPersistence giftingPersistence = new PlayerPrefsGiftingPersistence(identityCache);
            var pendingTransferService = new PendingTransferService(giftingPersistence, identityCache, wearableContainer.WearableCatalog, staticContainer.EmoteStorage);

            var selfProfile = new SelfProfile(profilesRepository, identityCache, equippedWearables, wearableContainer.WearableCatalog,
                staticContainer.EmoteStorage, equippedEmotes, selfEmotes, profileCache, globalWorld, playerEntity,
                pendingTransferService, forcedWearables);

            AddForcedWearablesWidget(staticContainer.DebugContainerBuilder, forcedWearables);

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

        /// <summary>
        ///     Equip/un-equip a wearable on the own avatar at runtime without owning it and without deploying it —
        ///     <see cref="ForcedWearables" /> strips them from every profile that goes to the catalyst.
        /// </summary>
        private static void AddForcedWearablesWidget(IDebugContainerBuilder debugBuilder, ForcedWearables forcedWearables)
        {
            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.FORCED_WEARABLES)
                       ?.AddStringFieldWithConfirmation(string.Empty, "Equip URN", urn => forcedWearables.Add(new URN(urn)))
                        .AddStringFieldWithConfirmation(string.Empty, "Un-equip URN", urn => forcedWearables.Remove(new URN(urn)))
                        .AddSingleButton("Un-equip all", forcedWearables.Clear);
        }

        private static void ParseDebugUrns(IReadOnlyCollection<string>? debugUrns, List<URN> parsed)
        {
            if (debugUrns?.Count > 0)
                parsed.AddRange(debugUrns.Select(urn => new URN(urn)));
        }

        private static void ParseParamsUrns(IAppArgs appParams, string flag, List<URN> parsed)
        {
            if (appParams.TryGetValue(flag, out string? csv) && !string.IsNullOrEmpty(csv!))
                parsed.AddRange(csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(urn => new URN(urn)));
        }
    }
}
