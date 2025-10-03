using DCL.VoiceChat;
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

        public void SetDependencies(ThumbnailLoader newThumbnailLoader, CommunitiesBrowserStateService communitiesBrowserStateService, ICommunityCallOrchestrator orchestrator)
        {
            filteredCommunitiesView.SetDependencies(newThumbnailLoader, communitiesBrowserStateService, orchestrator);
            streamingCommunitiesView.SetDependencies(newThumbnailLoader, communitiesBrowserStateService, orchestrator);
        }

        public void SetActiveView(CommunitiesViews activeView)
        {
            switch (activeView)
            {
                case CommunitiesViews.FILTERED_COMMUNITIES:
                    streamingCommunitiesView.HideStreamingSection();
                    filteredCommunitiesView.SetResultsBackButtonVisible(true);
                    break;
                case CommunitiesViews.BROWSE_ALL_COMMUNITIES:
                    filteredCommunitiesView.SetResultsBackButtonVisible(false);
                    break;
            }
        }
    }
}
