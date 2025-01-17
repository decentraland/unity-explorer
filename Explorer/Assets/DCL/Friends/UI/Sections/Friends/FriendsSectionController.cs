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
            FriendListPagedRequestManager requestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, requestManager)
        {
            requestManager.JumpInClicked += JumpInClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.JumpInClicked -= JumpInClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        protected override void ElementClicked(Profile profile)
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
