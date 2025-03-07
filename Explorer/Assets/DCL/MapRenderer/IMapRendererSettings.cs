using DCL.AssetsProvision;
using DCL.Audio;
using DCL.MapRenderer.ComponentsFactory;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.PlayerMarker;
using DCL.MapRenderer.MapLayers.PointsOfInterest;
using DCL.MapRenderer.MapLayers.SearchResults;
using DCL.MapRenderer.MapLayers.UsersMarker;
using System;
using UnityEngine;

namespace DCL.MapRenderer
{
    public interface IMapRendererSettings
    {
        public const int ATLAS_CHUNK_SIZE = 1020;
        public const int PARCEL_SIZE = 20;

        // it is quite expensive to disable TextMeshPro so larger bounds should help keeping the right balance
        public const float CULLING_BOUNDS_IN_PARCELS = 10;

        MapRendererConfigurationRef MapRendererConfiguration { get; }

        MapCameraObjectRef MapCameraObject { get; }

        SpriteRendererRef AtlasChunk { get; }

        ParcelHighlightMarkerObjectRef ParcelHighlight { get; }

        PlayerMarkerObjectRef PlayerMarker { get; }

        PinMarkerRef PinMarker { get; }

        SceneOfInterestMarkerObjectRef SceneOfInterestMarker { get; }

        CategoryMarkerObjectRef CategoryMarker { get; }

        SearchResultMarkerObjectRef SearchResultMarker { get; }

        ClusterMarkerObjectRef ClusterMarker { get; }

        ClusterMarkerObjectRef SearchResultsClusterMarker { get; }

        CategoryIconMappingsSO CategoryIconMappings { get; }

        CategoryLayerIconMappingsSO CategoryLayerIconMappings { get; }

        HotUserMarkerRef UserMarker { get; }

        DottedLineRef DestinationPathLine { get; }

        PinMarkerRef PathDestinationPin { get; }

        AudioClipConfig ClickAudio { get; }

        AudioClipConfig HoverAudio { get; }

        [Serializable]
        public class SpriteRendererRef : ComponentReference<SpriteRenderer>
        {
            public SpriteRendererRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class DottedLineRef : ComponentReference<MapPathRenderer>
        {
            public DottedLineRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class PinMarkerRef : ComponentReference<PinMarkerObject>
        {
            public PinMarkerRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class HotUserMarkerRef : ComponentReference<HotUserMarkerObject>
        {
            public HotUserMarkerRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class PlayerMarkerObjectRef : ComponentReference<PlayerMarkerObject>
        {
            public PlayerMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class ParcelHighlightMarkerObjectRef : ComponentReference<ParcelHighlightMarkerObject>
        {
            public ParcelHighlightMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class SceneOfInterestMarkerObjectRef : ComponentReference<SceneOfInterestMarkerObject>
        {
            public SceneOfInterestMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class ClusterMarkerObjectRef : ComponentReference<ClusterMarkerObject>
        {
            public ClusterMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class SearchResultMarkerObjectRef : ComponentReference<SearchResultMarkerObject>
        {
            public SearchResultMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class CategoryMarkerObjectRef : ComponentReference<CategoryMarkerObject>
        {
            public CategoryMarkerObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class MapCameraObjectRef : ComponentReference<MapCameraObject>
        {
            public MapCameraObjectRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class MapRendererConfigurationRef : ComponentReference<MapRendererConfiguration>
        {
            public MapRendererConfigurationRef(string guid) : base(guid) { }
        }
    }
}
