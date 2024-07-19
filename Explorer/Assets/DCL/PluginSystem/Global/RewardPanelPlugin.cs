using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Notification;
using DCL.Notification.NotificationsBus;
using DCL.RewardPanel;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class RewardPanelPlugin : IDCLGlobalPlugin<RewardPanelPlugin.RewardPanelSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IWebRequestController webRequestController;

        public RewardPanelPlugin(IMVCManager mvcManager, IAssetsProvisioner assetsProvisioner, INotificationsBusController notificationsBusController, IWebRequestController webRequestController)
        {
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.notificationsBusController = notificationsBusController;
            this.webRequestController = webRequestController;

            this.notificationsBusController.SubscribeToNotificationTypeReceived(NotificationType.GOVERNANCE_ANNOUNCEMENT, OnNewRewardReceived);
        }

        private void OnNewRewardReceived(INotification notification)
        {
            IncomingRewardNotification incomingRewardNotification = (IncomingRewardNotification)notification;

            mvcManager.ShowAsync(RewardPanelController.IssueCommand(
                new RewardPanelParameter(
                    notification.GetThumbnail(),
                    incomingRewardNotification.Metadata.Name,
                    incomingRewardNotification.Metadata.Rarity))
            ).Forget();
        }

        public async UniTask InitializeAsync(RewardPanelSettings settings, CancellationToken ct)
        {
            NFTColorsSO nftRarityColors = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);
            NftTypeIconSO rarityBackgroundsMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityBackgroundsMapping, ct);
            RewardPanelView rewardPanelView = (await assetsProvisioner.ProvideMainAssetAsync(settings.RewardPanelView, ct: ct)).Value.GetComponent<RewardPanelView>();
            RewardPanelController rewardPanelController = new RewardPanelController(RewardPanelController.CreateLazily(rewardPanelView, null), webRequestController, nftRarityColors, rarityBackgroundsMapping);
            mvcManager.RegisterController(rewardPanelController);
        }

        public void Dispose()
        {

        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public class RewardPanelSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public RewardPanelViewRef RewardPanelView { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

            [Serializable]
            public class RewardPanelViewRef : ComponentReference<RewardPanelView>
            {
                public RewardPanelViewRef(string guid) : base(guid) { }
            }
        }
    }
}
