using System;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Mathematics;

namespace ECS
{
    /// <summary>
    ///     World manifest from asset-bundle-registry (e.g. /worlds/{realmName}/manifest).
    ///     Form: { "occupied": ["0,0", "10,10"], "spawn_coordinate": { "x": 0, "y": 0 }, "total": 2 }
    ///     When non-empty, always has parsed parcel sets. Create from JSON via <see cref="WorldManifestDto" /> and <see cref="Create" />. Do not dispose returned sets.
    /// </summary>
    [Serializable]
    public struct WorldManifest : IDisposable
    {
        public int total;
        public SpawnCoordinateData spawn_coordinate;
        private NativeHashSet<int2> occupiedParcels;
        private bool isEmpty;

        public WorldManifest(int2[] valueOccupiedParcels)
        {
            total = 0;
            spawn_coordinate = new SpawnCoordinateData();
            occupiedParcels = ParcelArraysToSet(valueOccupiedParcels);
            isEmpty = false;
        }

        public bool IsEmpty => isEmpty;

        /// <summary>
        ///     Creates a WorldManifest with parsed parcel sets from a DTO. Call after JSON deserialization into <see cref="WorldManifestDto" />.
        /// </summary>
        public static WorldManifest Create(WorldManifestDto dto)
        {
            if (IsNullOrEmpty(dto.roads) && IsNullOrEmpty(dto.occupied) && IsNullOrEmpty(dto.empty))
                return Empty;

            return new WorldManifest
            {
                total = dto.total,
                spawn_coordinate = dto.spawn_coordinate,
                occupiedParcels = ParseParcelStringsToSet(dto.occupied),
                isEmpty = false
            };
        }

        private static bool IsNullOrEmpty(string[]? a) => a == null || a.Length == 0;

        public NativeHashSet<int2> GetOccupiedParcels() => occupiedParcels;

        private static NativeHashSet<int2> ParseParcelStringsToSet(string[]? parcelStrings)
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

        public void Dispose()
        {
            if (isEmpty)
                return;
            if (occupiedParcels.IsCreated) occupiedParcels.Dispose();
        }

        private static readonly NativeHashSet<int2> EMPTY_SET = new (0, Allocator.Persistent);

        public static WorldManifest Empty => new ()
        {
            occupiedParcels = EMPTY_SET,
            isEmpty = true,
            spawn_coordinate = new SpawnCoordinateData()
        };
    }

    [Serializable]
    public class SpawnCoordinateData
    {
        public int x;
        public int y;
    }

    /// <summary>
    ///     DTO for JSON deserialization of world manifest. Use <see cref="WorldManifest.Create" /> to obtain a <see cref="WorldManifest" /> with parsed sets.
    /// </summary>
    [Serializable]
    public class WorldManifestDto
    {
        public string[] roads;
        public string[] occupied;
        public string[] empty;
        public int total;

        public SpawnCoordinateData spawn_coordinate;
    }
}
