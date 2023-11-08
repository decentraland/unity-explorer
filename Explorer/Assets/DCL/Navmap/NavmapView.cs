using DCL.UI;
using DCLServices.MapRenderer.ConsumerUtils;
using UnityEngine;

namespace DCL.Navmap
{
    public class NavmapView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject satellite;

        [field: SerializeField]
        public GameObject streetView;

        [field: SerializeField]
        public NavmapZoomView zoomView;

        [field: SerializeField]
        public TabSelectorView[] TabSelectorViews { get; private set; }

        [field: SerializeField]
        public MapRenderImage SatelliteRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider SatellitePixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        public MapRenderImage StreetViewRenderImage { get; private set; }

        [field: SerializeField]
        public PixelPerfectMapRendererTextureProvider StreetViewPixelPerfectMapRendererTextureProvider { get; private set; }

        [field: SerializeField]
        public MapCameraDragBehavior.MapCameraDragBehaviorData MapCameraDragBehaviorData { get; private set; }
    }
}
