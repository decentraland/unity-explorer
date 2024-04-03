using System;

namespace DCL.MapRenderer.MapLayers
{
    [Flags]
    public enum MapLayer
    {
        None,
        ParcelsAtlas = 1,
        SatelliteAtlas = 1 << 1,
        ScenesOfInterest = 1 << 3,
        ParcelHoverHighlight = 1 << 6,
        Favorites = 1 << 7,
        PlayerMarker = 1 << 9,
        // Add yours
    }
}
