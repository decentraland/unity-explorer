using Arch.Core;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Optimization.Pools;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Tables
{
    /// <summary>
    ///     NOT Threadsafe
    /// </summary>
    public class EntityParticipantTable : IEntityParticipantTable
    {
        private readonly Dictionary<string, IReadOnlyEntityParticipantTable.Entry> walletIdToEntity = new (PoolConstants.AVATARS_COUNT);

        private readonly Dictionary<Entity, string> entityToWalletId = new (PoolConstants.AVATARS_COUNT);

        public int Count => walletIdToEntity.Count;

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
            if (!walletIdToEntity.TryAdd(walletId, new IReadOnlyEntityParticipantTable.Entry(walletId, entity, fromRoom)))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Wallet {walletId} is already registered to an entity, the new entity {entity} is not registered");
                return;
            }

            if (!entityToWalletId.TryAdd(entity, walletId))
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Entity {entity} is already registered to another wallet, it is not mapped back to {walletId}");
        }

        public void AddRoomSource(string walletId, RoomSource fromRoom)
        {
            if (!walletIdToEntity.TryGetValue(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Cannot add room source {fromRoom} to the unregistered wallet {walletId}");
                return;
            }

            walletIdToEntity[walletId] = entry.WithRoomSource(fromRoom);
        }

        public bool Release(string walletId, RoomSource fromRoom)
        {
            if (!walletIdToEntity.TryGetValue(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, $"Cannot release room source {fromRoom} from the unregistered wallet {walletId}");
                return false;
            }

            entry = entry.WithoutRoomSource(fromRoom);

            if (entry.ConnectedTo == RoomSource.None)
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
