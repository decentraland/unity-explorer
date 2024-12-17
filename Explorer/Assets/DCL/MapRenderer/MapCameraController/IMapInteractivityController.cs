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

        void ExitRenderImage();

        /// <summary>
        /// Returns Parcel corresponding to the given (cursor) position within UI `RawImage`
        /// </summary>
        bool TryGetParcel(Vector2 normalizedCoordinates, out Vector2Int parcel);

        Vector2 GetNormalizedPosition(Vector2Int parcel);

        GameObject? ProcessMousePosition(Vector2 worldPosition, Vector2 screenPosition);

        GameObject? ProcessMouseClick(Vector2 worldPosition, Vector2Int parcel);
    }
}
