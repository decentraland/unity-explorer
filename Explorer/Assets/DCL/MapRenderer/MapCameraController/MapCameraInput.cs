using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.MapLayers;
using UnityEngine;

namespace DCL.MapRenderer.MapCameraController
{
    public readonly struct MapCameraInput
    {
        public readonly MapLayer EnabledLayers;
        public readonly Vector2Int Position;
        public readonly float Zoom;
        public readonly Vector2Int TextureResolution;
        public readonly Vector2Int ZoomValues;
        public readonly IMapActivityOwner ActivityOwner;

        /// <param name="enabledLayers">active layers</param>
        /// <param name="position">default position</param>
        /// <param name="zoom">default zoom</param>
        /// <param name="textureResolution">desired texture resolution</param>
        /// <param name="zoomValues">zoom thresholds in parcels</param>
        public MapCameraInput(IMapActivityOwner activityOwner, MapLayer enabledLayers, Vector2Int position, float zoom,
            Vector2Int textureResolution, Vector2Int zoomValues)
        {
            EnabledLayers = enabledLayers;
            Position = position;
            Zoom = zoom;
            TextureResolution = textureResolution;
            ZoomValues = zoomValues;
            ActivityOwner = activityOwner;
        }
    }
}
