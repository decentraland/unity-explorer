using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Landscape.Parcel
{
    public class LandscapeParcelData
    {
        /// <summary>
        ///     Road parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2> RoadParcels { get; private set; }

        /// <summary>
        ///     Occupied parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2> OccupiedParcels { get; private set; }

        /// <summary>
        ///     Empty parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2> EmptyParcels { get; private set; }

        public LandscapeParcelData()
        {
            RoadParcels = new NativeHashSet<int2>();
            OccupiedParcels = new NativeHashSet<int2>();
            EmptyParcels = new NativeHashSet<int2>();
        }

        public void Reconfigure(NativeHashSet<int2> roadParcels,
            NativeHashSet<int2> occupiedParcels,
            NativeHashSet<int2> emptyParcels)
        {
            this.RoadParcels = roadParcels;
            this.OccupiedParcels = occupiedParcels;
            this.EmptyParcels = emptyParcels;
        }

        public NativeList<int2> GetEmptyParcelsList()
        {
            var list = new NativeList<int2>(Allocator.Persistent);
            foreach (var item in EmptyParcels)
                list.Add(item);
            return list;
        }
    }
}
