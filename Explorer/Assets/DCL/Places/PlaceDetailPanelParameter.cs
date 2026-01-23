using DCL.PlacesAPIService;

namespace DCL.Places
{
    public class PlaceDetailPanelParameter
    {
        public readonly PlacesData.PlaceInfo PlaceData;

        public PlaceDetailPanelParameter(PlacesData.PlaceInfo placeData)
        {
            this.PlaceData = placeData;
        }
    }
}
