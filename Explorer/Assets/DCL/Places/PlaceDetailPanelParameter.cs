using DCL.PlacesAPIService;

namespace DCL.Places
{
    public class PlaceDetailPanelParameter
    {
        public readonly PlacesData.PlaceInfo PlaceData;
        public readonly PlaceCardView? SummonerPlaceCard;

        public PlaceDetailPanelParameter(PlacesData.PlaceInfo placeData, PlaceCardView? summonerPlaceCard = null)
        {
            this.PlaceData = placeData;
            this.SummonerPlaceCard = summonerPlaceCard;
        }
    }
}
