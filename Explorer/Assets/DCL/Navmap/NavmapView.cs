using DCL.Audio;
using DCL.MapRenderer.ConsumerUtils;
using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Navmap
{
    public class NavmapView : MonoBehaviour
    {
        [field: SerializeField]
        public SearchBarView SearchBarView;

        [field: SerializeField]
        public SearchResultPanelView SearchBarResultPanel;

        [field: SerializeField]
        public HistoryRecordPanelView HistoryRecordPanelView;

        [FormerlySerializedAs("eventInfoCard")]
        [FormerlySerializedAs("floatingPanelView")]
        [field: SerializeField]
        public EventInfoCardView eventInfoCardView;

        [field: SerializeField]
        public NavmapFilterView filterView;

        [field: SerializeField]
        public NavmapZoomView zoomView;

        [field: SerializeField]
        public MapPinTooltipView MapPinTooltip;

        [field: SerializeField]
        public NavmapLocationView LocationView { get; private set; }

        [field: SerializeField]
        public NavmapPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; }

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        public MapCameraDragBehavior.MapCameraDragBehaviorData MapCameraDragBehaviorData { get; private set; }

        [field: SerializeField]
        public Animator PanelAnimator { get; private set; }

        [field: SerializeField]
        public Animator HeaderAnimator { get; private set; }

        [field: SerializeField]
        public WarningNotificationView WorldsWarningNotificationView { get; private set; }

        [field: SerializeField]
        public DestinationInfoElement DestinationInfoElement { get; private set; }

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }
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
