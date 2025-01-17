using Cysharp.Threading.Tasks;
using DCL.Profiles;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.Sections.Requests
{
    public class RequestsSectionController : FriendPanelSectionController<RequestsSectionView, RequestsRequestManager, RequestUserView>
    {
        public RequestsSectionController(RequestsSectionView view,
            IFriendsService friendsService,
            IFriendsEventBus friendEventBus,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager,
            RequestsRequestManager friendListPagedRequestManager)
            : base(view, friendsService, friendEventBus, web3IdentityCache, mvcManager, friendListPagedRequestManager)
        {
            friendListPagedRequestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public override void Dispose()
        {
            base.Dispose();
            friendListPagedRequestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void ContextMenuClicked(Profile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.UserId}");
        }

        protected override async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);

            friendListInitCts = friendListInitCts.SafeRestart();
            await friendListPagedRequestManager.Init(ct);

            view.SetLoadingState(false);

            view.LoopList.SetListItemCount(friendListPagedRequestManager.GetElementsNumber(), false);
            view.LoopList.RefreshAllShownItem();
            friendListPagedRequestManager.FirstFolderClicked += FolderClicked;
            friendListPagedRequestManager.SecondFolderClicked += FolderClicked;
        }

        protected override void FriendElementClicked(Profile profile)
        {

        }

    }
}
