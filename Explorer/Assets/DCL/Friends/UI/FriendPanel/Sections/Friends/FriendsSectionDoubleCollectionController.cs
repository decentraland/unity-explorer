using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Web3.Identities;
using MVC;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel.Sections.Friends
{
    public class FriendsSectionDoubleCollectionController : FriendPanelSectionDoubleCollectionController<FriendsSectionView, FriendListPagedDoubleCollectionRequestManager, FriendListUserView>
    {
        public FriendsSectionDoubleCollectionController(FriendsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            FriendListPagedDoubleCollectionRequestManager doubleCollectionRequestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, doubleCollectionRequestManager)
        {
            doubleCollectionRequestManager.JumpInClicked += JumpInClicked;
            doubleCollectionRequestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            requestManager.JumpInClicked -= JumpInClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        protected override void ElementClicked(FriendProfile profile)
        {
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.Address.ToString()))).Forget();
        }

        private void JumpInClicked(FriendProfile profile)
        {
            Debug.Log($"JumpInClicked on {profile.Address.ToString()}");
        }

        private void ContextMenuClicked(FriendProfile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.Address.ToString()}");
        }
    }
}
