using System;

namespace DCL.Multiplayer.FacialExpression
{
    /// <summary>
    ///     Intention to apply a facial expression to a remote avatar. Each index is a 0-15 tile
    ///     in the corresponding 4x4 atlas (ADR-317). Out-of-range values MUST be ignored at receive time.
    /// </summary>
    public readonly struct RemoteFacialExpressionIntention : IEquatable<RemoteFacialExpressionIntention>
    {
        public readonly string WalletId;
        public readonly byte EyebrowsIndex;
        public readonly byte EyesIndex;
        public readonly byte MouthIndex;

        public RemoteFacialExpressionIntention(string walletId, byte eyebrowsIndex, byte eyesIndex, byte mouthIndex)
        {
            WalletId = walletId;
            EyebrowsIndex = eyebrowsIndex;
            EyesIndex = eyesIndex;
            MouthIndex = mouthIndex;
        }

        public bool Equals(RemoteFacialExpressionIntention other) =>
            WalletId == other.WalletId
            && EyebrowsIndex == other.EyebrowsIndex
            && EyesIndex == other.EyesIndex
            && MouthIndex == other.MouthIndex;

        public override bool Equals(object? obj) =>
            obj is RemoteFacialExpressionIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletId, EyebrowsIndex, EyesIndex, MouthIndex);
    }
}