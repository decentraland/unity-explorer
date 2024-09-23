using System;

namespace DCL.MapRenderer.MapLayers
{
    [Flags]
    public enum MapLayer
    {
        None,
        SatelliteAtlas = 1 << 1,
        ScenesOfInterest = 1 << 3,
        HotUsersMarkers = 1 << 4,
        ParcelHoverHighlight = 1 << 6,
        Favorites = 1 << 7,
        Pins = 1 << 8,
        PlayerMarker = 1 << 9,
        Path = 1 << 10,

        // Add yours
    }
}
