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

        private void UnblockUserClicked(Profile profile)
        {
            Debug.Log($"UnblockUserClicked on {profile.UserId}");
        }

        private void ContextMenuClicked(Profile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.UserId}");
        }

        protected override void ElementClicked(Profile profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();

    }
}
