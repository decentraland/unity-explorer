using System;
using System.Collections.Generic;

namespace DCL.MapRenderer.MapLayers
{
    [Flags]
    public enum MapLayer
    {
        None,
        ParcelsAtlas = 1,
        SatelliteAtlas = 1 << 1,
        ScenesOfInterest = 1 << 3,
        HotUsersMarkers = 1 << 4,
        ParcelHoverHighlight = 1 << 6,
        Favorites = 1 << 7,
        Category = 1 << 8,
        Pins = 1 << 9,
        PlayerMarker = 1 << 10,
        Path = 1 << 11,
        SearchResults = 1 << 12,
        LiveEvents = 1 << 13,
        HomeMarker = 1 << 14,
        // Add yours
    }
}
