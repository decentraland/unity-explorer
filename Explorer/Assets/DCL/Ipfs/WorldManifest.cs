using System;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;

namespace DCL.Ipfs
{
    /// <summary>
    ///     World manifest from asset-bundle-registry (e.g. /worlds/{realmName}/manifest).
    ///     Form: { "occupied": ["0,0", "10,10"], "spawn_coordinate": { "x": 0, "y": 0 }, "total": 2 }
    ///     Parsed parcel sets are built when creating the struct via <see cref="WithParsedSets" />. Do not dispose returned sets.
    /// </summary>
    [Serializable]
    public struct WorldManifest : IDisposable
    {
        public string[] roads;
        public string[] occupied;
        public string[] empty;
        public int total;

        [JsonProperty("spawn_coordinate")]
        public SpawnCoordinateData? spawn_coordinate;

        [JsonIgnore]
        private NativeHashSet<int2> roadParcels;

        [JsonIgnore]
        private NativeHashSet<int2> occupiedParcels;

        [JsonIgnore]
        private NativeHashSet<int2> emptyParcels;

        [JsonIgnore]
        private bool isEmpty;

        public WorldManifest(int2[] fallbackParcelData, int2[] valueEmptyParcels, int2[] valueRoadParcels)
        {
            roads = null;
            occupied = null;
            empty = null;
            total = 0;
            spawn_coordinate = null;
            roadParcels = ParcelArraysToSet(valueRoadParcels);
            occupiedParcels = ParcelArraysToSet(fallbackParcelData);
            emptyParcels = ParcelArraysToSet(valueEmptyParcels);
            isEmpty = false;
        }

        public bool IsEmpty => isEmpty;

        /// <summary>
        ///     Returns a new WorldManifest with parsed parcel sets from the raw string arrays. Call after JSON deserialization.
        /// </summary>
        public static WorldManifest WithParsedSets(in WorldManifest raw)
        {
            if (IsNullOrEmpty(raw.roads) && IsNullOrEmpty(raw.occupied) && IsNullOrEmpty(raw.empty))
                return Empty;

            return new WorldManifest
            {
                roads = raw.roads,
                occupied = raw.occupied,
                empty = raw.empty,
                total = raw.total,
                spawn_coordinate = raw.spawn_coordinate,
                roadParcels = ParseParcelStrings(raw.roads),
                occupiedParcels = ParseParcelStrings(raw.occupied),
                emptyParcels = ParseParcelStrings(raw.empty),
                isEmpty = false
            };
        }

        private static bool IsNullOrEmpty(string[]? a) => a == null || a.Length == 0;

        public NativeHashSet<int2> GetRoadParcels() => roadParcels;
        public NativeHashSet<int2> GetOccupiedParcels() => occupiedParcels;
        public NativeHashSet<int2> GetEmptyParcels() => emptyParcels;

        private static NativeHashSet<int2> ParseParcelStrings(string[]? parcelStrings)
        {
            if (parcelStrings == null || parcelStrings.Length == 0)
                return new NativeHashSet<int2>(0, Allocator.Persistent);

            var hashSet = new NativeHashSet<int2>(parcelStrings.Length, Allocator.Persistent);
            foreach (string s in parcelStrings)
            {
                if (string.IsNullOrEmpty(s)) continue;
                string[] parts = s.Split(',');
                if (parts.Length >= 2 && int.TryParse(parts[0].Trim(), out int x) && int.TryParse(parts[1].Trim(), out int y))
                    hashSet.Add(new int2(x, y));
            }
            return hashSet;
        }

        private static NativeHashSet<int2> ParcelArraysToSet(int2[]? parcelArray)
        {
            if (parcelArray == null || parcelArray.Length == 0)
                return new NativeHashSet<int2>(0, Allocator.Persistent);
            var set = new NativeHashSet<int2>(parcelArray.Length, Allocator.Persistent);
            foreach (int2 p in parcelArray)
                set.Add(p);
            return set;
        }

        public NativeList<int2> GetEmptyParcelsList()
        {
            NativeHashSet<int2> set = emptyParcels;
            var list = new NativeList<int2>(set.Count, Allocator.Persistent);
            foreach (int2 item in set)
                list.Add(item);
            return list;
        }

        public void Dispose()
        {
            if (isEmpty)
                return;
            if (roadParcels.IsCreated) roadParcels.Dispose();
            if (occupiedParcels.IsCreated) occupiedParcels.Dispose();
            if (emptyParcels.IsCreated) emptyParcels.Dispose();
        }

        private static readonly NativeHashSet<int2> EMPTY_SET = new (0, Allocator.Persistent);

        public static WorldManifest Empty => new ()
        {
            roadParcels = EMPTY_SET,
            occupiedParcels = EMPTY_SET,
            emptyParcels = EMPTY_SET,
            isEmpty = true
        };

        [Serializable]
        public class SpawnCoordinateData
        {
            public int x;
            public int y;
        }
    }
}
