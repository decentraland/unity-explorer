using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Profiles;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.Sections.Friends
{
    public class FriendsSectionController : FriendPanelSectionController<FriendsSectionView, FriendListPagedRequestManager, FriendListUserView>
    {
        public FriendsSectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            FriendListPagedRequestManager friendListPagedRequestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, friendListPagedRequestManager)
        {
            friendListPagedRequestManager.JumpInClicked += JumpInClicked;
            friendListPagedRequestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            friendListPagedRequestManager.JumpInClicked -= JumpInClicked;
            friendListPagedRequestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        protected override void FriendElementClicked(Profile profile)
        {
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();
        }

        private void JumpInClicked(Profile profile)
        {
            Debug.Log($"JumpInClicked on {profile.UserId}");
        }

        private void ContextMenuClicked(Profile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.UserId}");
        }
    }
}
