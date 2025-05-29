using DCL.UI;
using DCL.UI.Utilities;
using SuperScrollView;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserView : MonoBehaviour
    {
        public event Action ViewAllMyCommunitiesButtonClicked;
        public event Action ResultsBackButtonClicked;
        public event Action<string> SearchBarSelected;
        public event Action<string> SearchBarDeselected;
        public event Action<string> SearchBarValueChanged;
        public event Action<string> SearchBarSubmit;
        public event Action SearchBarClearButtonClicked;
        public event Action<Vector2> ResultsLoopGridScrollChanged;

        [Header("Animators")]
        [SerializeField] private Animator panelAnimator;
        [SerializeField] private Animator headerAnimator;

        [Header("Search")]
        [SerializeField] private SearchBarView searchBar;

        [Header("Creation Section")]
        [SerializeField] private Button createCommunityButton;

        [Header("My Communities Section")]
        [SerializeField] private GameObject myCommunitiesSection;
        [SerializeField] private GameObject myCommunitiesMainContainer;
        [SerializeField] private GameObject myCommunitiesEmptyContainer;
        [SerializeField] private GameObject myCommunitiesLoadingSpinner;
        [SerializeField] private LoopListView2 myCommunitiesLoopList;
        [SerializeField] private Button myCommunitiesViewAllButton;

        [Header("Results Section")]
        [SerializeField] private Button resultsBackButton;
        [SerializeField] private TMP_Text resultsTitleText;
        [SerializeField] private TMP_Text resultsCountText;
        [SerializeField] private GameObject resultsSection;
        [SerializeField] private LoopGridView resultLoopGrid;
        [SerializeField] private GameObject resultsEmptyContainer;
        [SerializeField] private GameObject resultsLoadingSpinner;
        [SerializeField] private GameObject resultsLoadingMoreSpinner;

        private void Awake()
        {
            myCommunitiesViewAllButton.onClick.AddListener(() => ViewAllMyCommunitiesButtonClicked?.Invoke());
            resultsBackButton.onClick.AddListener(() => ResultsBackButtonClicked?.Invoke());
            searchBar.inputField.onSelect.AddListener(text => SearchBarSelected?.Invoke(text));
            searchBar.inputField.onDeselect.AddListener(text => SearchBarDeselected?.Invoke(text));
            searchBar.inputField.onValueChanged.AddListener(text =>
            {
                SearchBarValueChanged?.Invoke(text);
                SetSearchBarClearButtonActive(!string.IsNullOrEmpty(text));
            });
            searchBar.inputField.onSubmit.AddListener(text => SearchBarSubmit?.Invoke(text));
            searchBar.clearSearchButton.onClick.AddListener(() => SearchBarClearButtonClicked?.Invoke());
        }

        private void Start() =>
            resultLoopGrid.ScrollRect.onValueChanged.AddListener(pos => ResultsLoopGridScrollChanged?.Invoke(pos));

        private void OnDestroy()
        {
            myCommunitiesViewAllButton.onClick.RemoveAllListeners();
            resultsBackButton.onClick.RemoveAllListeners();
            searchBar.inputField.onSelect.RemoveAllListeners();
            searchBar.inputField.onDeselect.RemoveAllListeners();
            searchBar.inputField.onValueChanged.RemoveAllListeners();
            searchBar.inputField.onSubmit.RemoveAllListeners();
            searchBar.clearSearchButton.onClick.RemoveAllListeners();
            resultLoopGrid.ScrollRect.onValueChanged.RemoveAllListeners();
        }

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            panelAnimator.SetTrigger(triggerId);
            headerAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            panelAnimator.Rebind();
            headerAnimator.Rebind();
            panelAnimator.Update(0);
            headerAnimator.Update(0);
        }

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

        public void SetResultsBackButtonVisible(bool isVisible) =>
            resultsBackButton.gameObject.SetActive(isVisible);

        public void SetResultsTitleText(string text) =>
            resultsTitleText.text = text;

        public void SetResultsCountText(int count) =>
            resultsCountText.text = $"({count})";

        public void SetResultsLoadingMoreActive(bool isActive) =>
            resultsLoadingMoreSpinner.SetActive(isActive);

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

        public void InitializeMyCommunitiesList(int itemTotalCount, Func<LoopListView2, int, LoopListViewItem2> onGetItemByIndex)
        {
            myCommunitiesLoopList.InitListView(itemTotalCount, onGetItemByIndex);
            myCommunitiesLoopList.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void SetMyCommunitiesLoopListItemCount(int itemCount, bool resetPos = true) =>
            myCommunitiesLoopList.SetListItemCount(itemCount, resetPos);

        public void InitializeResultsGrid(int itemTotalCount, Func<LoopGridView,int,int,int, LoopGridViewItem> onGetItemByRowColumn)
        {
            resultLoopGrid.InitGridView(itemTotalCount, onGetItemByRowColumn);
            resultLoopGrid.gameObject.GetComponent<ScrollRect>()?.SetScrollSensitivityBasedOnPlatform();
        }

        public void SetResultsLoopGridItemCount(int itemCount, bool resetPos = true) =>
            resultLoopGrid.SetListItemCount(itemCount, resetPos);

        public void RefreshResultsLoopGridItemByItemIndex(int itemIndex) =>
            resultLoopGrid.RefreshItemByItemIndex(itemIndex);

        public float GetResultsLoopGridVerticalNormalizedPosition() =>
            resultLoopGrid.ScrollRect.verticalNormalizedPosition;

        private void SetSearchBarClearButtonActive(bool isActive) =>
            searchBar.clearSearchButton.gameObject.SetActive(isActive);
    }
}
