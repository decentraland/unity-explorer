using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.Input;
using DCL.Passport;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.WebRequests;
using ECS;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class PassportPlugin : DCLGlobalPluginBase<PassportPlugin.PassportSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private PassportController passportController;
        private readonly ICursor cursor;
        private readonly IProfileRepository profileRepository;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly ISelfProfile selfProfile;

        public PassportPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            ICursor cursor,
            IProfileRepository profileRepository,
            ICharacterPreviewFactory characterPreviewFactory,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IRealmData realmData,
            URLDomain assetBundleURL,
            IWebRequestController webRequestController,
            CharacterPreviewEventBus characterPreviewEventBus,
            ISelfProfile selfProfile)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.cursor = cursor;
            this.profileRepository = profileRepository;
            this.characterPreviewFactory = characterPreviewFactory;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.realmData = realmData;
            this.assetBundleURL = assetBundleURL;
            this.webRequestController = webRequestController;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.selfProfile = selfProfile;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(PassportSettings passportSettings, CancellationToken ct)
        {
            (NFTColorsSO rarityColorMappings, NftTypeIconSO categoryIconsMapping, NftTypeIconSO rarityBackgroundsMapping, NftTypeIconSO rarityInfoPanelBackgroundsMapping) = await UniTask.WhenAll(
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityColorMappings, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.CategoryIconsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityBackgroundsMapping, ct),
                assetsProvisioner.ProvideMainAssetValueAsync(passportSettings.RarityInfoPanelBackgroundsMapping, ct));

            PassportView chatView = (await assetsProvisioner.ProvideMainAssetAsync(passportSettings.PassportPrefab, ct: ct)).Value.GetComponent<PassportView>();

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                ECSThumbnailProvider thumbnailProvider = new ECSThumbnailProvider(realmData, builder.World, assetBundleURL, webRequestController);

                passportController = new PassportController(
                    PassportController.CreateLazily(chatView, null),
                    cursor,
                    profileRepository,
                    characterPreviewFactory,
                    chatEntryConfiguration,
                    rarityBackgroundsMapping,
                    rarityColorMappings,
                    categoryIconsMapping,
                    characterPreviewEventBus,
                    mvcManager,
                    selfProfile,
                    builder.World,
                    arguments.PlayerEntity,
                    thumbnailProvider);

                mvcManager.RegisterController(passportController);
            };
        }

        public override void Dispose()
        {
            passportController.Dispose();
            base.Dispose();
        }

        public class PassportSettings : IDCLPluginSettings
        {
            [field: Header(nameof(PassportPlugin) + "." + nameof(PassportSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject PassportPrefab;

            [field: SerializeField]
            public AssetReferenceT<NFTColorsSO> RarityColorMappings { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityInfoPanelBackgroundsMapping { get; set; }
        }
    }
}
