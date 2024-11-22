using DCL.PlacesAPIService;
using System.Collections.Generic;
using UnityEngine;

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

        public static Vector2Int GetParcelsCenter(PlacesData.PlaceInfo sceneInfo)
        {
            Vector2 centerTile = Vector2.zero;

            for (var i = 0; i < sceneInfo.Positions.Length; i++)
            {
                Vector2Int parcel = sceneInfo.Positions[i];
                centerTile += parcel;
            }

            centerTile /= sceneInfo.Positions.Length;
            float distance = float.PositiveInfinity;
            Vector2Int centerParcel = Vector2Int.zero;

            for (var i = 0; i < sceneInfo.Positions.Length; i++)
            {
                var parcel = sceneInfo.Positions[i];

                if (Vector2.Distance(centerTile, parcel) < distance)
                {
                    distance = Vector2Int.Distance(centerParcel, parcel);
                    centerParcel = parcel;
                }
            }

            return centerParcel;
        }
    }
}
