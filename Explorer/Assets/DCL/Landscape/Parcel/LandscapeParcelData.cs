using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace DCL.Landscape.Parcel
{
    /// <summary>
    /// TODO
    /// Class owns native resources. Their liftimes must be clarified and reflected in the semantics.
    /// </symmary>
    public class LandscapeParcelData
    {
        private static NativeHashSet<int2>.ReadOnly EMPTY_SET = new NativeHashSet<int2>().AsReadOnly();

        private NativeHashSet<int2> roadParcels;
        private NativeHashSet<int2> occupiedParcels;
        private NativeHashSet<int2> emptyParcels;

        /// <summary>
        ///     Road parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2>.ReadOnly RoadParcels 
        {
            get
            {
                if (roadParcels.IsCreated)
                    return roadParcels.AsReadOnly();

                return EMPTY_SET;
            }
        }

        /// <summary>
        ///     Occupied parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2>.ReadOnly OccupiedParcels
        {
            get
            {
                if (occupiedParcels.IsCreated)
                    return occupiedParcels.AsReadOnly();

                return EMPTY_SET;
            }
        }

        /// <summary>
        ///     Empty parcels from WorldManifest.json
        /// </summary>
        public NativeHashSet<int2> EmptyParcels => emptyParcels;

        public LandscapeParcelData()
        {
            roadParcels = new NativeHashSet<int2>();
            occupiedParcels = new NativeHashSet<int2>();
            emptyParcels = new NativeHashSet<int2>();
        }

        public void Reconfigure(NativeHashSet<int2> roadParcels,
            NativeHashSet<int2> occupiedParcels,
            NativeHashSet<int2> emptyParcels)
        {
            Assert.IsTrue(roadParcels.IsCreated);
            Assert.IsTrue(occupiedParcels.IsCreated);
            Assert.IsTrue(emptyParcels.IsCreated);
            this.roadParcels = roadParcels;
            this.occupiedParcels = occupiedParcels;
            this.emptyParcels = emptyParcels;
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
