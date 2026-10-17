using DCL.Audio;
using DCL.ExplorePanel;
using DCL.MapRenderer.ConsumerUtils;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Navmap
{
    public class NavmapView : MonoBehaviour
    {
        [field: SerializeField]
        public List<CategoryToggleView> categoryToggles = null!;

        [field: SerializeField]
        public SearchBarView SearchBarView = null!;

        [field: SerializeField]
        public SearchResultPanelView SearchBarResultPanel = null!;

        [field: SerializeField]
        public PlacesAndEventsPanelView PlacesAndEventsPanelView { get; private set; } = null!;

        [field: SerializeField]
        public PlaceInfoToastView PlaceToastView { get; private set; } = null!;

        [field: SerializeField]
        public SharePlacesAndEventsContextMenuView ShareContextMenuView { get; private set; } = null!;

        [field: SerializeField]
        public HistoryRecordPanelView HistoryRecordPanelView = null!;

        [field: SerializeField]
        public NavmapZoomView zoomView = null!;

        [field: SerializeField]
        public MapPinTooltipView MapPinTooltip = null!;

        [field: SerializeField]
        public NavmapLocationView LocationView { get; private set; } = null!;

        [field: SerializeField]
        public NavmapPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; } = null!;

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; } = null!;

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; } = null!;

        [field: SerializeField]
        public MapCameraDragBehavior.MapCameraDragBehaviorData MapCameraDragBehaviorData { get; private set; } = null!;

        [field: SerializeField]
        public Animator PanelAnimator { get; private set; } = null!;

        [field: SerializeField]
        public WarningNotificationView WorldsWarningNotificationView { get; private set; } = null!;

        [field: SerializeField]
        public DestinationInfoElement DestinationInfoElement { get; private set; } = null!;

        [field: SerializeField]
        public NavmapCategoriesStripView CategoriesStrip { get; private set; } = null!;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; } = null!;

        private bool isCompact;

        public void SetCompactLayout(bool compact)
        {
            if (isCompact == compact) return;

            isCompact = compact;

            float sign = compact ? 1f : -1f;
            var searchBar = (RectTransform)SearchBarView.transform;
            var band = (RectTransform)searchBar.parent;

            // Centred in its band, so the close slot comes off the right edge and the bar drops onto the modal header's centre line.
            searchBar.sizeDelta -= new Vector2(sign * ExploreSectionModalView.CLOSE_BUTTON_SLOT_WIDTH, 0f);
            searchBar.anchoredPosition -= new Vector2(sign * ExploreSectionModalView.CLOSE_BUTTON_SLOT_WIDTH / 2f, sign * (ExploreSectionModalView.HEADER_HEIGHT - band.rect.height) / 2f);

            CategoriesStrip.SetWrapping(compact);
        }
    }

    [Serializable]
    public struct NavmapPanelTabSelectorMapping
    {
        [field: SerializeField]
        public TabSelectorView TabSelectorViews { get; private set; }

        [field: SerializeField]
        public NavmapSections Section { get; private set; }
    }
}
