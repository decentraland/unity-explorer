using UnityEngine;

namespace DCL.Landscape.Settings
{
    [CreateAssetMenu(fileName = "ParcelLoadingFilteringSettings", menuName = "DCL/Landscape/Parcel Loading Filtering Settings")]
    public class ParcelLoadingFilteringSettings : ScriptableObject
    {
        [Header("Parcel Filtering")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool excludeEmptyParcels = true;
        [SerializeField] private bool excludeRoadParcels = true;
        [SerializeField] private bool excludeOccupiedParcels = false;

        public bool Enabled => enabled;
        public bool ExcludeEmptyParcels => excludeEmptyParcels;
        public bool ExcludeRoadParcels => excludeRoadParcels;
        public bool ExcludeOccupiedParcels => excludeOccupiedParcels;
    }
}
