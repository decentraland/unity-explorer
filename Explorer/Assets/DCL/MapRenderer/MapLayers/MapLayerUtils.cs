using System.Collections.Generic;

namespace DCL.MapRenderer.MapLayers
{
    public static class MapLayerUtils
    {
        public static Dictionary<MapLayer, string> MapLayerToCategory = new Dictionary<MapLayer, string>()
        {
            {MapLayer.Art, "art"},
            {MapLayer.Crypto, "crypto"},
            {MapLayer.Social, "social"},
            {MapLayer.Game, "game"},
            {MapLayer.Shop, "shop"},
            {MapLayer.Education, "education"},
            {MapLayer.Music, "music"},
            {MapLayer.Fashion, "fashion"},
            {MapLayer.Casino, "casino"},
            {MapLayer.Sports, "sports"},
            {MapLayer.Business, "business"},
        };
    }
}
