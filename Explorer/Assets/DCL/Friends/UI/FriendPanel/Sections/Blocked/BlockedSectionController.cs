using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedSectionController : FriendPanelSectionController<BlockedSectionView, BlockedRequestManager, BlockedUserView>
    {
        private readonly IPassportBridge passportBridge;

        public BlockedSectionController(BlockedSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            BlockedRequestManager requestManager,
            IPassportBridge passportBridge) : base(view, web3IdentityCache, requestManager)
        {
            this.passportBridge = passportBridge;

            requestManager.UnblockClicked += UnblockUserClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.UnblockClicked -= UnblockUserClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void UnblockUserClicked(FriendProfile profile)
        {
            ReportHub.Log(LogType.Error, new ReportData(ReportCategory.FRIENDS), $"Unblock user button clicked for {profile.Address.ToString()}. Users should not be able to reach this");
        }

        private void ContextMenuClicked(FriendProfile profile)
        {
            ReportHub.Log(LogType.Error, new ReportData(ReportCategory.FRIENDS), $"Context menu on blocked user button clicked for {profile.Address.ToString()}. Users should not be able to reach this");
        }

        protected override void ElementClicked(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

    }
}
