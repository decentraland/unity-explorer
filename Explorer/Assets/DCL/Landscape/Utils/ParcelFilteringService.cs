using DCL.Landscape.Parcel;
using DCL.Landscape.Settings;
using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Landscape.Utils
{
    public class ParcelFilteringService
    {
        private readonly LandscapeParcelData landscapeParcelData;
        private readonly ParcelLoadingFilteringSettings settings;

        public ParcelFilteringService(LandscapeParcelData landscapeParcelData,
            ParcelLoadingFilteringSettings settings)
        {
            this.landscapeParcelData = landscapeParcelData;
            this.settings = settings;
        }

        public bool ShouldIncludeParcel(int2 parcel)
        {
            if (!settings.Enabled) return true;

            return !(settings.ExcludeEmptyParcels && IsEmptyParcel(parcel))
                   && !(settings.ExcludeRoadParcels && IsRoadParcel(parcel))
                   && !(settings.ExcludeOccupiedParcels && IsOccupiedParcel(parcel));
        }

        private bool IsEmptyParcel(int2 parcel) => landscapeParcelData.EmptyParcels.Contains(parcel);
        private bool IsRoadParcel(int2 parcel) => landscapeParcelData.RoadParcels.Contains(parcel);
        private bool IsOccupiedParcel(int2 parcel) => landscapeParcelData.OccupiedParcels.Contains(parcel);
    }
}
