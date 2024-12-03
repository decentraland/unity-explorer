using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Entities;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IReadOnlyEntityParticipantTable
    {
        public struct Entry
        {
            public string WalletId;
            public Entity Entity;
            public RoomSource ConnectedTo;

            internal Entry(string walletId, Entity entity, RoomSource connectedTo)
            {
                WalletId = walletId;
                Entity = entity;
                ConnectedTo = connectedTo;
            }
        }

        int Count { get; }

        Entry Get(string walletId);

        bool TryGet(string walletId, out Entry entry);

        bool Has(string walletId);

        IReadOnlyCollection<string> Wallets();
    }
}
