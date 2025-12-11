using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.NotificationsBus;
using DCL.NotificationsBus.NotificationTypes;
using DCL.RewardPanel;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using DCL.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class RewardPanelPlugin : IDCLGlobalPlugin<RewardPanelPlugin.RewardPanelSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly NotificationsBusController notificationsBusController;
        private readonly UITextureProvider textureProvider;

        public RewardPanelPlugin(IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            UITextureProvider textureProvider)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.textureProvider = textureProvider;

            NotificationsBusController.Instance.SubscribeToNotificationTypeReceived(NotificationType.REWARD_IN_PROGRESS, OnNewRewardReceived);
        }

        private void OnNewRewardReceived(INotification notification)
        {
            RewardInProgressNotification rewardInProgressNotification = (RewardInProgressNotification)notification;

            mvcManager.ShowAsync(RewardPanelController.IssueCommand(
                           new RewardPanelParameter(
                               notification.GetThumbnail(),
                               rewardInProgressNotification.Metadata.Name,
                               rewardInProgressNotification.Metadata.Rarity,
                               rewardInProgressNotification.Metadata.Category))
                       )
                      .Forget();
        }

        public async UniTask InitializeAsync(RewardPanelSettings settings, CancellationToken ct)
        {
            NFTColorsSO nftRarityColors = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);
            NftTypeIconSO rarityBackgroundsMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgroundsMapping, ct);
            NftTypeIconSO categoryIconsMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.CategoryIconsMapping, ct);
            RewardPanelView rewardPanelView = (await assetsProvisioner.ProvideMainAssetAsync(settings.RewardPanelView, ct: ct)).Value.GetComponent<RewardPanelView>();
            var rewardPanelController = new RewardPanelController(RewardPanelController.CreateLazily(rewardPanelView, null), textureProvider, nftRarityColors, rarityBackgroundsMapping, categoryIconsMapping);
            mvcManager.RegisterController(rewardPanelController);
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class RewardPanelSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public RewardPanelViewRef RewardPanelView { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; }

            [Serializable]
            public class RewardPanelViewRef : ComponentReference<RewardPanelView>
            {
                public RewardPanelViewRef(string guid) : base(guid) { }
            }
        }
    }
}
