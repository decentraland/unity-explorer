using DCL.MapRenderer.ConsumerUtils;
using DCL.UI;
using System;
using TMPro;
using UnityEngine;

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

        [field: SerializeField]
        public FloatingPanelView floatingPanelView;

        [field: SerializeField]
        public NavmapFilterView filterView;

        [field: SerializeField]
        public NavmapZoomView zoomView;

        [field: SerializeField]
        public NavmapLocationView LocationView { get; private set; }

        [field: SerializeField]
        public NavmapPanelTabSelectorMapping[] TabSelectorMappedViews { get; private set; }

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        public MapRenderImage StreetViewRenderImage { get; private set; }

        [field: SerializeField]
        public MapCameraDragBehavior.MapCameraDragBehaviorData MapCameraDragBehaviorData { get; private set; }
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
