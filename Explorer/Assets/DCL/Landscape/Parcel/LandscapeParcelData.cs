using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Landscape.Parcel
{
    public class LandscapeParcelData
    {
        /// <summary>
        ///     Road parcels from WorldManifest.json
        /// </summary>
        public NativeParallelHashSet<int2> RoadParcels { get; private set; }

        /// <summary>
        ///     Occupied parcels from WorldManifest.json
        /// </summary>
        public NativeParallelHashSet<int2> OccupiedParcels { get; private set; }

        /// <summary>
        ///     Empty parcels from WorldManifest.json
        /// </summary>
        public NativeList<int2> EmptyParcels { get; private set; }

        public LandscapeParcelData()
        {
            RoadParcels = new NativeParallelHashSet<int2>();
            OccupiedParcels = new NativeParallelHashSet<int2>();
            EmptyParcels = new NativeList<int2>();
        }

        public void Reconfigure(NativeParallelHashSet<int2> roadParcels,
            NativeParallelHashSet<int2> occupiedParcels,
            NativeList<int2> emptyParcels)
        {
            this.RoadParcels = roadParcels;
            this.OccupiedParcels = occupiedParcels;
            this.EmptyParcels = emptyParcels;
        }
    }
}
