using System;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Ipfs
{
    /// <summary>
    ///     World manifest from asset-bundle-registry (e.g. /worlds/{realmName}/manifest).
    ///     Form: { "occupied": ["0,0", "10,10"], "spawn_coordinate": { "x": 0, "y": 0 }, "total": 2 }
    /// </summary>
    [Serializable]
    public struct WorldManifest
    {
        public Vector2[] roads;
        public Vector2[] occupied;
        public Vector2[] empty;
        public int total;

        [JsonProperty("spawn_coordinate")]
        public SpawnCoordinateData? spawn_coordinate;

        [Serializable]
        public class SpawnCoordinateData
        {
            public int x;
            public int y;
        }

        public NativeHashSet<int2> GetRoadParcels()
        {
            var hashSet = new NativeHashSet<int2>(roads.Length, Allocator.Persistent);

            foreach (Vector2 parcel in roads)
                hashSet.Add(new int2(parcel));

            return hashSet;
        }

        public NativeHashSet<int2> GetOccupiedParcels()
        {
            var hashSet = new NativeHashSet<int2>(occupied.Length, Allocator.Persistent);

            foreach (Vector2 parcel in occupied)
                hashSet.Add(new int2(parcel));

            return hashSet;
        }

        public NativeHashSet<int2> GetEmptyParcels()
        {
            var hashSet = new NativeHashSet<int2>(empty.Length, Allocator.Persistent);

            foreach (Vector2 emptyParcel in empty)
                hashSet.Add(new int2(emptyParcel));

            return hashSet;
        }
    }
}
