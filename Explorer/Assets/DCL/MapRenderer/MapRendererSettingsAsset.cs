using DCL.Audio;
using DCL.MapRenderer.MapLayers.Categories;
using UnityEngine;

namespace DCL.MapRenderer
{
    [CreateAssetMenu(fileName = "MapRendererSettings", menuName = "DCL/Map/Map Renderer Settings Asset")]
    public class MapRendererSettingsAsset : ScriptableObject, IMapRendererSettings
    {
        [field: SerializeField]
        public IMapRendererSettings.MapRendererConfigurationRef MapRendererConfiguration { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.MapCameraObjectRef MapCameraObject { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.SpriteRendererRef AtlasChunk { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.ParcelHighlightMarkerObjectRef ParcelHighlight { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.PlayerMarkerObjectRef PlayerMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.HomeMarkerObjectRef HomeMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.PinMarkerRef PinMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.SceneOfInterestMarkerObjectRef SceneOfInterestMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.SearchResultMarkerObjectRef SearchResultMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.CategoryMarkerObjectRef CategoryMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.ClusterMarkerObjectRef ClusterMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.ClusterMarkerObjectRef SearchResultsClusterMarker { get; private set; }

        [field: SerializeField]
        public CategoryIconMappingsSO CategoryIconMappings { get; private set; }

        [field: SerializeField]
        public CategoryLayerIconMappingsSO CategoryLayerIconMappings { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.HotUserMarkerRef UserMarker { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.DottedLineRef DestinationPathLine { get; private set; }

        [field: SerializeField]
        public IMapRendererSettings.PinMarkerRef PathDestinationPin { get; private set; }

        [field: SerializeField]
        public AudioClipConfig ClickAudio { get; private set; }

        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }
    }
}
