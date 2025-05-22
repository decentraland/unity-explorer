using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.UI.Utilities;
using DCL.WebRequests;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using CommunityData = DCL.Communities.GetUserCommunitiesResponse.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly ISelfProfile selfProfile;
        private readonly IWebRequestController webRequestController;
        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();

        private CancellationTokenSource loadMyCommunitiesCts;
        private CancellationTokenSource loadResultsCts;

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            ICommunitiesDataProvider dataProvider,
            ISelfProfile selfProfile,
            IWebRequestController webRequestController)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;
            this.selfProfile = selfProfile;
            this.webRequestController = webRequestController;

            ConfigureMyCommunitiesList();
            ConfigureResultsGrid();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            cursor.Unlock();

            loadMyCommunitiesCts = loadMyCommunitiesCts.SafeRestart();
            LoadMyCommunitiesAsync(loadMyCommunitiesCts.Token).Forget();

            loadResultsCts = loadResultsCts.SafeRestart();
            LoadResultsAsync(loadResultsCts.Token).Forget();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText("Decentraland Communities");
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
            loadMyCommunitiesCts?.SafeCancelAndDispose();
        }

        public void Animate(int triggerId)
        {
            view.panelAnimator.SetTrigger(triggerId);
            view.headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.panelAnimator.Rebind();
            view.headerAnimator.Rebind();
            view.panelAnimator.Update(0);
            view.headerAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Dispose()
        {
            loadMyCommunitiesCts?.SafeCancelAndDispose();
        }

        private void ConfigureMyCommunitiesList()
        {
            view.myCommunitiesLoopList.InitListView(0, SetupMyCommunityCardByIndex);
            view.myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        private void ConfigureResultsGrid()
        {
            view.resultLoopGrid.InitGridView(0, SetupCommunityResultCardByIndex);
            view.resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        private LoopListViewItem2 SetupMyCommunityCardByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);

            MyCommunityCardView cardView = listItem.GetComponent<MyCommunityCardView>();
            cardView.SetTitle(currentMyCommunities[index].name);
            cardView.SetUserRole(currentMyCommunities[index].role);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(currentMyCommunities[index].thumbnails[0]);

            return listItem;
        }

        private LoopGridViewItem SetupCommunityResultCardByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);

            CommunityResultCardView cardView = gridItem.GetComponent<CommunityResultCardView>();
            cardView.SetTitle(currentResults[index].name);
            cardView.ConfigureImageController(webRequestController);
            cardView.SetCommunityThumbnail(currentResults[index].thumbnails[0]);

            return gridItem;
        }

        private async UniTask LoadMyCommunitiesAsync(CancellationToken ct)
        {
            currentMyCommunities.Clear();
            view.myCommunitiesLoopList.SetListItemCount(0, false);
            view.SetMyCommunitiesAsLoading(true);

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(ownProfile.UserId, isOwner: true, isMember: true, pageNumber: 1, elementsPerPage: 1000, ct);

            foreach (CommunityData community in userCommunitiesResponse.communities)
                currentMyCommunities.Add(community);

            view.SetMyCommunitiesAsLoading(false);
            view.myCommunitiesLoopList.SetListItemCount(userCommunitiesResponse.communities.Length, false);
            view.SetMyCommunitiesAsEmpty(userCommunitiesResponse.communities.Length == 0);
        }

        private async UniTask LoadResultsAsync(CancellationToken ct)
        {
            currentResults.Clear();
            view.resultLoopGrid.SetListItemCount(0, false);
            view.SetResultsAsLoading(true);

            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile == null)
                return;

            var userCommunitiesResponse = await dataProvider.GetUserCommunitiesAsync(ownProfile.UserId, isOwner: false, isMember: false, pageNumber: 1, elementsPerPage: 200, ct);

            foreach (CommunityData community in userCommunitiesResponse.communities)
                currentResults.Add(community);

            view.SetResultsAsLoading(false);
            view.resultLoopGrid.SetListItemCount(userCommunitiesResponse.communities.Length, false);
            view.SetResultsAsEmpty(userCommunitiesResponse.communities.Length == 0);
        }
    }
}
