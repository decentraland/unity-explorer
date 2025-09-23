using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Landscape.ManifestParcel
{
    public class ManifestParcelData
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
        public NativeParallelHashSet<int2> EmptyParcels { get; private set; }

        public bool Configured { get; private set; }

        public ManifestParcelData()
        {
            RoadParcels = new NativeParallelHashSet<int2>();
            OccupiedParcels = new NativeParallelHashSet<int2>();
            EmptyParcels = new NativeParallelHashSet<int2>();

            Configured = false;
        }

        public void Reconfigure(NativeParallelHashSet<int2> roadParcels,
            NativeParallelHashSet<int2> occupiedParcels,
            NativeParallelHashSet<int2> emptyParcels)
        {
            this.RoadParcels = roadParcels;
            this.OccupiedParcels = occupiedParcels;
            this.EmptyParcels = emptyParcels;

            Configured = true;
        }
    }
}
