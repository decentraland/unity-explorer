using DCL.Input;
using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesResponse.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserController : ISection, IDisposable
    {
        private readonly CommunitiesBrowserView view;
        private readonly RectTransform rectTransform;
        private readonly ICursor cursor;
        private readonly ICommunitiesDataProvider dataProvider;
        private readonly List<CommunityData> currentMyCommunities = new ();
        private readonly List<CommunityData> currentResults = new ();

        public CommunitiesBrowserController(
            CommunitiesBrowserView view,
            ICursor cursor,
            ICommunitiesDataProvider dataProvider)
        {
            this.view = view;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();
            this.cursor = cursor;
            this.dataProvider = dataProvider;

            ConfigureSortBySelector();

            view.myCommunitiesLoopList.InitListView(0, OnGetMyCommunitiesItemByIndex);
            view.myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();

            view.resultLoopGrid.InitGridView(0, OnGetResultsItemByIndex);
            view.resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void Activate()
        {
            view.gameObject.SetActive(true);
            cursor.Unlock();

            view.SetResultsBackButtonVisible(false);
            view.SetResultsTitleText("Decentraland Communities");
            LoadMyCommunities();
            LoadResults();
        }

        public void Deactivate()
        {
            view.gameObject.SetActive(false);
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

        }

        private void ConfigureSortBySelector()
        {
            view.sortByDropdown.Dropdown.interactable = true;
            view.sortByDropdown.Dropdown.MultiSelect = false;
            view.sortByDropdown.Dropdown.options.Clear();
            view.sortByDropdown.Dropdown.options.AddRange(new[]
            {
                new TMP_Dropdown.OptionData { text = "Alphabetically" },
                new TMP_Dropdown.OptionData { text = "Popularity" },
            });
            view.sortByDropdown.Dropdown.value = 0;
        }

        private LoopListViewItem2 OnGetMyCommunitiesItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            return listItem;
        }

        private void LoadMyCommunities()
        {
            currentMyCommunities.Clear();
            view.myCommunitiesLoopList.SetListItemCount(0, false);

            List<CommunityData> requestCommunities = new List<CommunityData>();
            for (var i = 1; i <= 20; i++)
            {
                requestCommunities.Add(new CommunityData
                {
                    id = i.ToString(),
                    name = $"My Community {i}",
                    role = CommunityMemberRole.member,
                });
            }

            foreach (CommunityData community in requestCommunities)
                currentMyCommunities.Add(community);

            view.myCommunitiesLoopList.SetListItemCount(requestCommunities.Count, false);
        }

        private LoopGridViewItem OnGetResultsItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem gridItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            return gridItem;
        }

        private void LoadResults()
        {
            currentResults.Clear();
            view.resultLoopGrid.SetListItemCount(0, false);

            List<CommunityData> requestCommunities = new List<CommunityData>();
            for (var i = 1; i <= 20; i++)
            {
                requestCommunities.Add(new CommunityData
                {
                    id = i.ToString(),
                    name = $"Result Community {i}",
                    role = CommunityMemberRole.member,
                });
            }

            foreach (CommunityData community in requestCommunities)
                currentResults.Add(community);

            view.resultLoopGrid.SetListItemCount(requestCommunities.Count, false);
        }
    }
}
