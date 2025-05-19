using Cysharp.Threading.Tasks;
using DCL.Friends;
using DCL.Profiles;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : IDisposable
    {
        private const int PAGE_SIZE = 20;
        private const int ELEMENT_MISSING_THRESHOLD = 5;

        private readonly MembersListView view;
        private readonly ViewDependencies viewDependencies;
        private readonly List<Profile> members = new (PAGE_SIZE);

        private string lastCommunityId = string.Empty;
        private CancellationToken ct;
        private int pageNumber;
        private int totalFetched;
        private int totalToFetch;
        private bool isFetching;

        public MembersListController(MembersListView view,
            ViewDependencies viewDependencies)
        {
            this.view = view;
            this.viewDependencies = viewDependencies;

            this.view.LoopList.InitListView(0, GetLoopListItemByIndex);
        }

        public void Dispose()
        {
        }

        public void Reset()
        {
            lastCommunityId = string.Empty;
            members.Clear();
            pageNumber = 0;
            totalFetched = 0;
            totalToFetch = 0;
            isFetching = false;
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListRowItemView elementView = listItem.GetComponent<MemberListRowItemView>();

            elementView.ResetElements();

            int leftIndex = index * 2;
            int rightIndex = leftIndex + 1;

            if (rightIndex < members.Count)
                elementView.ConfigureRight(members[rightIndex], viewDependencies);

            elementView.ConfigureLeft(members[leftIndex], viewDependencies);

            elementView.SubscribeToInteractions(MainButtonClicked, ContextMenuButtonClicked, FriendButtonClicked);

            if (index >= totalFetched - ELEMENT_MISSING_THRESHOLD && totalFetched < totalToFetch && !isFetching)
                FetchNewDataAsync().Forget();

            return listItem;
        }

        private async UniTaskVoid FetchNewDataAsync()
        {
            isFetching = true;

            pageNumber++;
            await FetchDataAsync();
            totalFetched = (pageNumber + 1) * PAGE_SIZE;

            view.LoopList.SetListItemCount(Mathf.CeilToInt(members.Count * 1f / 2), false);
            view.LoopList.RefreshAllShownItem();

            isFetching = false;
        }

        private async UniTask FetchDataAsync()
        {
            // Simulate fetching data
            for (int i = 0; i < 15; i++)
                members.Add(Profile.NewRandomProfile(null));
            await UniTask.Delay(1000, cancellationToken: ct);
            totalToFetch = 1; //TODO: From the service response
        }

        public void ShowMembersListAsync(string communityId, CancellationToken cancellationToken)
        {
            if (lastCommunityId == null || lastCommunityId.Equals(communityId)) return;

            ct = cancellationToken;
            lastCommunityId = communityId;

            FetchNewDataAsync().Forget();
        }

        private void MainButtonClicked(Profile profile)
        {
            // Handle main button click
            Debug.Log("MainButtonClicked: " + profile.UserId);
        }

        private void ContextMenuButtonClicked(Profile profile)
        {
            // Handle context menu button click
            Debug.Log("ContextMenuButtonClicked: " + profile.UserId);
        }

        private void FriendButtonClicked(Profile profile, FriendshipStatus status)
        {
            // Handle friend button click
            Debug.Log("FriendButtonClicked: " + profile.UserId);
        }
    }
}
