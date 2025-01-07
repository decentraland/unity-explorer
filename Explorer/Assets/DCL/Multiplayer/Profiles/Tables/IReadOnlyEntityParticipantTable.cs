using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using System.Collections.Generic;
using Utility;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IReadOnlyEntityParticipantTable
    {
        public readonly struct Entry
        {
            public readonly string WalletId;
            public readonly Entity Entity;
            public readonly RoomSource ConnectedTo;

            internal Entry(string walletId, Entity entity, RoomSource connectedTo)
            {
                WalletId = walletId;
                Entity = entity;
                ConnectedTo = connectedTo;
            }

            public Entry WithRoomSource(RoomSource fromRoom) =>
                new (WalletId, Entity, ConnectedTo | fromRoom);

            public Entry WithoutRoomSource(RoomSource fromRoom)
            {
                RoomSource newRoomSource = ConnectedTo;
                newRoomSource.RemoveFlag(fromRoom);
                return new Entry(WalletId, Entity, newRoomSource);
            }
        }

        int Count { get; }

        Entry Get(string walletId);

        bool TryGet(string walletId, out Entry entry);

        bool Has(string walletId);

        IReadOnlyCollection<string> Wallets();
    }
}
