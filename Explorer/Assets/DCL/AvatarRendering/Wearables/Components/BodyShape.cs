using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using System;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Wearables
{
    public readonly struct BodyShape : IEquatable<BodyShape>
    {
        public const int COUNT = 2;

        public readonly string Value;
        public readonly int Index;

        private BodyShape(string value, int index)
        {
            Value = value;
            Index = index;
        }

        public static implicit operator string(BodyShape bodyShape) =>
            bodyShape.Value;

        public static implicit operator URN(BodyShape bodyShape) =>
            bodyShape.Value;

        public static implicit operator int(BodyShape bodyShape) =>
            bodyShape.Index;

        public static implicit operator BodyShape(PBAvatarShape pbAvatarShape)
        {
            if (pbAvatarShape.BodyShape == MALE.Value)
                return MALE;

            if (pbAvatarShape.BodyShape == FEMALE.Value)
                return FEMALE;

            ReportHub.LogError(ReportCategory.AVATAR, $"'{pbAvatarShape.BodyShape}' body shape not supported, using MALE instead.");

            return MALE;
        }

        public static BodyShape FromStringSafe(string bodyShape)
        {
            if (bodyShape == FEMALE.Value)
                return FEMALE;

            // Use male by default
            return MALE;
        }

        public static readonly BodyShape MALE = new ("urn:decentraland:off-chain:base-avatars:BaseMale", 0);
        public static readonly BodyShape FEMALE = new ("urn:decentraland:off-chain:base-avatars:BaseFemale", 1);

        public static readonly IReadOnlyList<BodyShape> VALUES = new[] { MALE, FEMALE };

        public bool Equals(BodyShape other) =>
            Value == other.Value && Index == other.Index;

        public override bool Equals(object obj) =>
            obj is BodyShape other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Value, Index);
    }
}
