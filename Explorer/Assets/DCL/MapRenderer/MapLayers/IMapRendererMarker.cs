using UnityEngine;

namespace DCL.MapRenderer.MapLayers
{
    public interface IMapRendererMarker
    {
        Vector2Int ParcelCoords { get; }
        Vector2 Pivot { get; }

        void ToggleSelection(bool isSelected);

        void SetIsSelected(bool isSelected) { }

    }
}
