using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Emotes
{
    public struct RemoteEmoteStopIntention
    {
        public readonly string WalletId;
        public readonly float Timestamp;
        public RemoteEmoteStopIntention(string walletId, float timestamp)
        {
            WalletId = walletId;
            Timestamp = timestamp;
        }

        public bool Equals(RemoteEmoteStopIntention other) =>
            WalletId == other.WalletId && Mathf.Approximately(Timestamp, other.Timestamp);

        public override bool Equals(object? obj) =>
            obj is RemoteEmoteStopIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WalletId, Timestamp);
    }
}
