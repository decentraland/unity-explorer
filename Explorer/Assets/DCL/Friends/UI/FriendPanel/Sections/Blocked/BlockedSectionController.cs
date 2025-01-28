using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Profiles;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Blocked
{
    public class BlockedSectionController : FriendPanelSectionController<BlockedSectionView, BlockedRequestManager, BlockedUserView>
    {
        private readonly IMVCManager mvcManager;

        public BlockedSectionController(BlockedSectionView view,
            IWeb3IdentityCache web3IdentityCache,
            BlockedRequestManager requestManager,
            IMVCManager mvcManager) : base(view, web3IdentityCache, requestManager)
        {
            this.mvcManager = mvcManager;

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
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.Address.ToString()))).Forget();

    }
}
