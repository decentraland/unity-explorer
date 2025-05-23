using DCL.UI;
using SuperScrollView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        [field: Header("Animators")]
        [field: SerializeField] internal Animator panelAnimator { get; private set; }
        [field: SerializeField] internal Animator headerAnimator { get; private set; }

        [field: Header("Search")]
        [field: SerializeField] internal SearchBarView searchBar { get; private set; }

        [field: Header("Creation Section")]
        [field: SerializeField] internal Button createCommunityButton { get; private set; }

        [field: Header("My Communities Section")]
        [field: SerializeField] internal GameObject myCommunitiesSection { get; private set; }
        [field: SerializeField] internal GameObject myCommunitiesMainContainer { get; private set; }
        [field: SerializeField] internal GameObject myCommunitiesEmptyContainer { get; private set; }
        [field: SerializeField] internal GameObject myCommunitiesLoadingSpinner { get; private set; }
        [field: SerializeField] internal LoopListView2 myCommunitiesLoopList { get; private set; }
        [field: SerializeField] internal Button myCommunitiesViewAllButton { get; private set; }

        [field: Header("Results Section")]
        [field: SerializeField] internal Button resultsBackButton { get; private set; }
        [field: SerializeField] internal TMP_Text resultsTitleText { get; private set; }
        [field: SerializeField] internal TMP_Text resultsCountText { get; private set; }
        [field: SerializeField] internal GameObject resultsSection { get; private set; }
        [field: SerializeField] internal LoopGridView resultLoopGrid { get; private set; }
        [field: SerializeField] internal GameObject resultsEmptyContainer { get; private set; }
        [field: SerializeField] internal GameObject resultsLoadingSpinner { get; private set; }
        [field: SerializeField] internal GameObject resultsLoadingMoreSpinner { get; private set; }

        public void SetMyCommunitiesAsLoading(bool isLoading)
        {
            myCommunitiesLoadingSpinner.SetActive(isLoading);
            myCommunitiesSection.SetActive(!isLoading);
        }

        public void SetMyCommunitiesAsEmpty(bool isEmpty)
        {
            myCommunitiesEmptyContainer.SetActive(isEmpty);
            myCommunitiesMainContainer.SetActive(!isEmpty);
        }

        public void SetResultsAsLoading(bool isLoading)
        {
            resultsLoadingSpinner.SetActive(isLoading);
            resultsSection.SetActive(!isLoading);

            if (isLoading)
                resultsCountText.text = string.Empty;
        }

        public void SetResultsAsEmpty(bool isEmpty)
        {
            resultsEmptyContainer.SetActive(isEmpty);
            resultLoopGrid.gameObject.SetActive(!isEmpty);
        }

        public void SetResultsBackButtonVisible(bool isVisible)
        {
            resultsBackButton.gameObject.SetActive(isVisible);
            resultsCountText.gameObject.SetActive(isVisible);
        }

        public void SetResultsTitleText(string text) =>
            resultsTitleText.text = text;

        public void SetResultsCountText(int count) =>
            resultsCountText.text = $"({count})";

        public void SetResultsLoadingMoreActive(bool isActive) =>
            resultsLoadingMoreSpinner.SetActive(isActive);

        public void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);

        public void CleanSearchBar(bool raiseOnChangeEvent = true)
        {
            TMP_InputField.OnChangeEvent originalEvent = searchBar.inputField.onValueChanged;

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = new TMP_InputField.OnChangeEvent();

            searchBar.inputField.text = string.Empty;
            SetSearchBarClearButtonActive(false);

            if (!raiseOnChangeEvent)
                searchBar.inputField.onValueChanged = originalEvent;
        }
    }
}
