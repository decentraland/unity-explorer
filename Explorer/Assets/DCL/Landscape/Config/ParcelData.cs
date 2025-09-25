using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Config
{
    [Serializable]
    public class ParcelData : ScriptableObject
    {
        public int2[] roadParcels;
        public int2[] ownedParcels;
        public int2[] emptyParcels;

        public NativeParallelHashSet<int2> GetRoadParcels()
        {
            var hashSet = new NativeParallelHashSet<int2>(roadParcels.Length, Allocator.Persistent);

            foreach (int2 parcel in roadParcels)
                hashSet.Add(new int2(parcel));

            return hashSet;
        }

        public NativeParallelHashSet<int2> GetOwnedParcels()
        {
            var hashSet = new NativeParallelHashSet<int2>(ownedParcels.Length, Allocator.Persistent);

            foreach (int2 parcel in ownedParcels)
                hashSet.Add(parcel);

            return hashSet;
        }

        public NativeList<int2> GetEmptyParcels()
        {
            var nativeList = new NativeList<int2>(emptyParcels.Length, Allocator.Persistent);

            foreach (int2 emptyParcel in emptyParcels)
                nativeList.Add(emptyParcel);

            return nativeList;
        }
    }
}
