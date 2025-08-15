using System;
using UnityEngine;
using UnityEngine.UI;
using CommunityData = DCL.Communities.GetUserCommunitiesData.CommunityData;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionFilterResultsView : MonoBehaviour
    {
        // Do all the search or filtered logic that it was doing before.
        // This could be reused for showing only streaming communities

        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action<Vector2>? ScrollChanged;

        public event Action? ResultsBackButtonClicked;

        [SerializeField] private Button backButton = null!;
        [SerializeField] private CommunitiesGridView communitiesGridView = null!;

        [SerializeField] private ScrollRect scrollRect = null!;

        private void Awake()
        {
            backButton.onClick.AddListener(OnResultsBackButtonClicked);
        }

        private void OnResultsBackButtonClicked()
        {
            ResultsBackButtonClicked?.Invoke();
        }

        private void OnDestroy()
        {
            backButton.onClick.RemoveAllListeners();
        }
    }
}
