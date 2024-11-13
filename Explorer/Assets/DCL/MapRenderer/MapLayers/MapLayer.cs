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
        Art = 1 << 8,
        Crypto = 1 << 9,
        Social = 1 << 10,
        Game = 1 << 11,
        Shop = 1 << 12,
        Education = 1 << 13,
        Music = 1 << 14,
        Fashion = 1 << 15,
        Casino = 1 << 16,
        Sports = 1 << 17,
        Business = 1 << 18,
        Pins = 1 << 19,
        PlayerMarker = 1 << 20,
        Path = 1 << 21,

        // Add yours
    }
}
