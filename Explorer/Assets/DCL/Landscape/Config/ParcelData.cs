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
        public int2[] ownedParcels;
        public int2[] emptyParcels;

        public NativeParallelHashSet<int2> GetOwnedParcels()
        {
            var hashSet = new NativeParallelHashSet<int2>(ownedParcels.Length, Allocator.Persistent);

            foreach (int2 parcel in ownedParcels)
                hashSet.Add(parcel);

            return hashSet;
        }

        public NativeArray<int2> GetEmptyParcels()
        {
            var array = new NativeArray<int2>(emptyParcels.Length, Allocator.Persistent);

            for (var i = 0; i < emptyParcels.Length; i++)
                array[i] = emptyParcels[i];

            return array;
        }
    }
}
