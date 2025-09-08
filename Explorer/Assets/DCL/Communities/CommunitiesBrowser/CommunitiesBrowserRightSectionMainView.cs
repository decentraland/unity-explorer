using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionMainView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;
        public event Action? LoopGridScrollChanged;

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
            switch (activeSection)
            {
                case CommunitiesSections.FILTERED_COMMUNITIES:
                    streamingCommunitiesView.HideStreamingSection();
                    filteredCommunitiesView.SetResultsBackButtonVisible(true);
                    filteredCommunitiesView.SetCountTextVisible(true);
                    break;
                case CommunitiesSections.BROWSE_ALL_COMMUNITIES:
                    filteredCommunitiesView.SetResultsBackButtonVisible(false);
                    filteredCommunitiesView.SetCountTextVisible(true);
                    break;
                case CommunitiesSections.REQUESTS_AND_INVITES:
                    streamingCommunitiesView.HideStreamingSection();
                    filteredCommunitiesView.SetResultsBackButtonVisible(false);
                    filteredCommunitiesView.SetCountTextVisible(false);
                    break;
            }
        }
    }
}
