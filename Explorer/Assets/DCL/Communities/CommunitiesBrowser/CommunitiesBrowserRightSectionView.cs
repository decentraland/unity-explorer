using DCL.UI.Profiles.Helpers;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        [SerializeField] private FilteredCommunitiesView filteredCommunitiesView = null!;
        [SerializeField] private StreamingCommunitiesView streamingCommunitiesView = null!;
        [SerializeField] private ScrollRect scrollRect = null!;

        public bool IsResultsScrollPositionAtBottom => scrollRect.verticalNormalizedPosition <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;

        public StreamingCommunitiesView StreamingCommunitiesView => streamingCommunitiesView;
        public FilteredCommunitiesView FilteredCommunitiesView => filteredCommunitiesView;

        private void Awake()
        {
            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
        }

        public event Action? LoopGridScrollChanged;

        private void OnScrollRectValueChanged(Vector2 _)
        {
            LoopGridScrollChanged?.Invoke();
        }

        public void SetDependencies(ThumbnailLoader newThumbnailLoader, CommunitiesBrowserStateService communitiesBrowserStateService)
        {
            filteredCommunitiesView.SetDependencies(newThumbnailLoader, communitiesBrowserStateService);
            streamingCommunitiesView.SetDependencies(newThumbnailLoader, communitiesBrowserStateService);
        }

        public void SetActiveSection(CommunitiesSections activeSection)
        {
            if (activeSection == CommunitiesSections.FILTERED_COMMUNITIES)
            {
                streamingCommunitiesView.ClearStreamingResultsItems();
                filteredCommunitiesView.SetResultsBackButtonVisible(true);
            }
            else filteredCommunitiesView.SetResultsBackButtonVisible(false);
        }
    }
}
