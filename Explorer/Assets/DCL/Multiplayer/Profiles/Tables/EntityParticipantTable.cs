using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using Utility;

namespace DCL.Multiplayer.Profiles.Tables
{
    /// <summary>
    ///     NOT Thread-safe
    /// </summary>
    public class EntityParticipantTable : IEntityParticipantTable
    {
        private readonly Dictionary<string, IReadOnlyEntityParticipantTable.Entry> walletIdToEntity = new (PoolConstants.AVATARS_COUNT);

        private readonly Dictionary<Entity, string> entityToWalletId = new (PoolConstants.AVATARS_COUNT);

        public int Count => walletIdToEntity.Count;

        public IReadOnlyEntityParticipantTable.Entry Get(string walletId)
        {
            try { return walletIdToEntity[walletId]; }
            catch (Exception e) { throw new Exception($"Cannot find entity for walletId: {walletId}", e); }
        }

        public bool TryGet(string walletId, out IReadOnlyEntityParticipantTable.Entry entry)
        {
            entry = default(IReadOnlyEntityParticipantTable.Entry);
            return !string.IsNullOrEmpty(walletId) && walletIdToEntity.TryGetValue(walletId, out entry);
        }

        public bool Has(string walletId) =>
            !string.IsNullOrEmpty(walletId) && walletIdToEntity.ContainsKey(walletId);

        public IReadOnlyCollection<string> Wallets() =>
            walletIdToEntity.Keys;

        public void Register(string walletId, Entity entity, RoomSource fromRoom)
        {
            walletIdToEntity.Add(walletId, new IReadOnlyEntityParticipantTable.Entry(walletId, entity, fromRoom));
            entityToWalletId.Add(entity, walletId);
        }

        public void AddRoomSource(string walletId, RoomSource fromRoom)
        {
            IReadOnlyEntityParticipantTable.Entry entry = walletIdToEntity[walletId];
            entry.ConnectedTo |= fromRoom;
            walletIdToEntity[walletId] = entry;
        }

        public bool Release(string walletId, RoomSource fromRoom)
        {
            IReadOnlyEntityParticipantTable.Entry entry = walletIdToEntity[walletId];

            entry.ConnectedTo.RemoveFlag(fromRoom);

            if (entry.ConnectedTo == RoomSource.NONE)
            {
                walletIdToEntity.Remove(walletId);
                entityToWalletId.Remove(entry.Entity);
                return true;
            }

            walletIdToEntity[walletId] = entry;
            return false;
        }
    }
}
