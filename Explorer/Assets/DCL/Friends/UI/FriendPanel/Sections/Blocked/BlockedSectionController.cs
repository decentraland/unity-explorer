using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedSectionController : FriendPanelSectionController<BlockedSectionView, BlockedRequestManager, BlockedUserView>
    {
        private readonly IMVCManager mvcManager;
        private readonly IPassportBridge passportBridge;

        public BlockedSectionController(BlockedSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            BlockedRequestManager requestManager,
            IMVCManager mvcManager,
            IPassportBridge passportBridge) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;
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
            Debug.Log($"UnblockUserClicked on {profile.Address.ToString()}");
        }

        private void ContextMenuClicked(FriendProfile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.Address.ToString()}");
        }

        protected override void ElementClicked(FriendProfile profile) =>
            passportBridge.ShowAsync(profile.Address).Forget();

    }
}
