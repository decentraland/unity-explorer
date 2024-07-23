using DCL.MapRenderer.MapLayers.Pins;
using UnityEngine;

namespace DCL.MapRenderer.MapCameraController
{
    public interface IMapInteractivityController
    {
        bool HighlightEnabled { get; }

        /// <summary>
        /// Highlights the (hovered) parcel
        /// </summary>
        void HighlightParcel(Vector2Int parcel);

        void RemoveHighlight();

        /// <summary>
        /// Returns Parcel corresponding to the given (cursor) position within UI `RawImage`
        /// </summary>
        bool TryGetParcel(Vector2 normalizedCoordinates, out Vector2Int parcel, out IPinMarker pinMarker);

        Vector2 GetNormalizedPosition(Vector2Int parcel);
    }
}
