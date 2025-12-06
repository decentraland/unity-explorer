
using System;
using UnityEngine;

namespace DCL.Multiplayer.Emotes
{
    /// <summary>
    /// Intention to force an avatar to look at a position.
    /// </summary>
    public readonly struct LookAtPositionIntention : IEquatable<LookAtPositionIntention>
    {
        public readonly string WalletAddress;
        public readonly Vector3 TargetPosition;

        public LookAtPositionIntention(string walletAddress, Vector3 targetPosition)
        {
            WalletAddress = walletAddress;
            TargetPosition = targetPosition;
        }

        public bool Equals(LookAtPositionIntention other) =>
            WalletAddress == other.WalletAddress &&
            TargetPosition == other.TargetPosition;

        public override bool Equals(object? obj) =>
            obj is LookAtPositionIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletAddress, TargetPosition);
    }
}
