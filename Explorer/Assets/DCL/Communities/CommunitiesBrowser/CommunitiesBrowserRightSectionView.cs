using System;
using UnityEngine;

namespace DCL.Communities.CommunitiesBrowser
{
    public class CommunitiesBrowserRightSectionView : MonoBehaviour
    {
        private const float NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE = 0.01f;

        public event Action<Vector2>? BrowseAllScrollChanged;
        public event Action<Vector2>? FilteredScrollChanged;

        private SectionType currentSectionType = SectionType.BROWSE_ALL_SECTION;

        [SerializeField] private CommunitiesBrowserRightSectionFilterResultsView filterResultsSection = null!;
        [SerializeField] private CommunitiesBrowserRightSectionBrowseAllView browseAllCommunitiesSection = null!;

        // This will depend on which view is visible, as the scroll rect we need to watch for changes.
        public bool IsResultsScrollPositionAtBottom()
        {
            var position = 0f;

            switch (currentSectionType)
            {
                case SectionType.FILTER_SECTION:
                    position = filterResultsSection.ScrollRect.verticalNormalizedPosition;
                    break;
                case SectionType.BROWSE_ALL_SECTION:
                    position = browseAllCommunitiesSection.ScrollRect.verticalNormalizedPosition;
                    break;
            }

            return position <= NORMALIZED_V_POSITION_OFFSET_FOR_LOADING_MORE;
        }

        private void Start()
        {
            browseAllCommunitiesSection.ScrollRect.onValueChanged.AddListener(pos => FilteredScrollChanged?.Invoke(pos));
            filterResultsSection.ScrollRect.onValueChanged.AddListener(pos => BrowseAllScrollChanged?.Invoke(pos));
        }
        private void OnDestroy()
        {
            browseAllCommunitiesSection.ScrollRect.onValueChanged.RemoveAllListeners();
            filterResultsSection.ScrollRect.onValueChanged.RemoveAllListeners();
        }

        private enum SectionType
        {
            FILTER_SECTION,
            BROWSE_ALL_SECTION
        }
    }
}
