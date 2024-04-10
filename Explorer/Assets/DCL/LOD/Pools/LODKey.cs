using System;
using Utility;

namespace DCL.LOD
{
    public readonly struct LODKey : IEquatable<LODKey>, IEquatable<LODAsset?>
    {
        /// <summary>
        ///     Original hash (of the scene)
        /// </summary>
        public readonly string Hash;

        public readonly byte Level;

        public LODKey(string hash, byte level)
        {
            Hash = hash.ToLower();
            Level = level;
        }

        public bool Equals(LODKey other) =>
            Hash.Equals(other.Hash, StringComparison.OrdinalIgnoreCase) && Level == other.Level;

        public bool Equals(LODAsset? other) =>
            other != null && Equals(other.Value.LodKey);

        public override bool Equals(object obj) =>
            obj is LODKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Hash), Level);

        public override string ToString() =>
            $"{Hash}_{Level}";
    }
}
