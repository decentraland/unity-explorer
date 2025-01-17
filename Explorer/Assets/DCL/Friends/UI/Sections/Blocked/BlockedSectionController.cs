using Cysharp.Threading.Tasks;
using DCL.Passport;
using DCL.Profiles;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using SuperScrollView;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Friends.UI.Sections.Blocked
{
    public class BlockedSectionController : IDisposable
    {
        private readonly BlockedSectionView view;
        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly BlockedRequestManager requestManager;

        private Web3Address? previousWeb3Identity;
        private CancellationTokenSource blockedUsersInitCts = new ();

        public BlockedSectionController(BlockedSectionView view,
            IMVCManager mvcManager,
            IProfileRepository profileRepository,
            IProfileCache profileCache,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.web3IdentityCache = web3IdentityCache;

            this.view.Enable += Enable;
            this.view.Disable += Disable;
            requestManager = new BlockedRequestManager(profileRepository, profileCache, web3IdentityCache);
            this.view.LoopList.InitListView(0, OnGetItemByIndex);
            requestManager.ElementClicked += BlockedUserClicked;
            requestManager.UnblockClicked += UnblockUserClicked;
            requestManager.ContextMenuClicked += ContextMenuClicked;
        }

        public void Dispose()
        {
            view.Enable -= Enable;
            view.Disable -= Disable;
            requestManager.ElementClicked -= BlockedUserClicked;
            requestManager.UnblockClicked -= UnblockUserClicked;
            requestManager.ContextMenuClicked -= ContextMenuClicked;
        }

        private void BlockedUserClicked(Profile profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.UserId))).Forget();

        private void UnblockUserClicked(Profile profile)
        {
            Debug.Log($"UnblockUserClicked on {profile.UserId}");
        }

        private void ContextMenuClicked(Profile profile)
        {
            Debug.Log($"ContextMenuClicked on {profile.UserId}");
        }

        private void Enable()
        {
            previousWeb3Identity ??= web3IdentityCache.Identity?.Address;

            if (previousWeb3Identity != web3IdentityCache.Identity?.Address)
            {
                previousWeb3Identity = web3IdentityCache.Identity?.Address;
                requestManager.Reset();
            }

            if (!requestManager.WasInitialised)
                Init(blockedUsersInitCts.Token).Forget();
        }

        private LoopListViewItem2 OnGetItemByIndex(LoopListView2 loopListView, int index) =>
            requestManager.GetLoopListItemByIndex(loopListView, index);

        private async UniTaskVoid Init(CancellationToken ct)
        {
            view.SetLoadingState(true);
            view.SetEmptyState(false);
            view.SetScrollViewState(false);

            blockedUsersInitCts = blockedUsersInitCts.SafeRestart();
            await requestManager.Init(ct);

            view.SetEmptyState(!requestManager.HasElements);
            view.SetLoadingState(false);
            view.SetScrollViewState(requestManager.HasElements);

            if (requestManager.HasElements)
                view.LoopList.SetListItemCount(requestManager.GetElementsNumber(), false);
        }

        private void Disable()
        {
            blockedUsersInitCts = blockedUsersInitCts.SafeRestart();
        }
    }
}
