// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KtxUnity.Editor
{

    readonly struct UnityVersion : IComparable<UnityVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        public readonly char Type;
        public readonly int Sequence;


        const string k_Pattern = @"^(?<major>\d{1,10})(\.(?<minor>\d{1,10}))?(\.(?<patch>\d{1,10}))?(?<type>[abf])?(?<sequence>\d{1,10})?";

        static readonly Regex k_Regex = new Regex(k_Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMinutes(1));

        public UnityVersion(string version)
        {
            var match = k_Regex.Match(version);

            if (!match.Success)
                throw new InvalidOperationException($"Failed to parse semantic version {version}");

            Major = int.Parse(match.Groups["major"].Value);
            var minorGroup = match.Groups["minor"];
            Minor = minorGroup.Success
                ? int.Parse(minorGroup.Value)
                : 0;
            var patchGroup = match.Groups["patch"];
            Patch = patchGroup.Success
                ? int.Parse(patchGroup.Value)
                : 0;

            var typeGroup = match.Groups["type"];
            Type = typeGroup.Success
                ? typeGroup.Value[0]
                : 'f';

            var sequenceGroup = match.Groups["sequence"];
            Sequence = sequenceGroup.Success
                ? int.Parse(sequenceGroup.Value)
                : 1;
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}{Type}{Sequence}";
        }

        public int CompareTo(UnityVersion other)
        {
            if (Major != other.Major)
            {
                return Major.CompareTo(other.Major);
            }

            if (Minor != other.Minor)
            {
                return Minor.CompareTo(other.Minor);
            }

            if (Patch != other.Patch)
            {
                return Patch.CompareTo(other.Patch);
            }

            if (Type != other.Type)
            {
                return Type.CompareTo(other.Type);
            }

            return Sequence.CompareTo(other.Sequence);
        }

        public static bool operator <(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) == 0;
        }

        public static bool operator !=(UnityVersion left, UnityVersion right)
        {
            return left.CompareTo(right) != 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is UnityVersion other)
            {
                return CompareTo(other) == 0;
            }

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Major.GetHashCode();
                hash = hash * 23 + Minor.GetHashCode();
                hash = hash * 23 + Patch.GetHashCode();
                hash = hash * 23 + Type.GetHashCode();
                hash = hash * 23 + Sequence.GetHashCode();
                return hash;
            }
        }
    }
}
