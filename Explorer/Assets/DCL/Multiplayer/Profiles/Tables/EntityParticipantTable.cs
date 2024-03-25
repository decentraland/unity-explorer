using Arch.Core;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Tables
{
    /// <summary>
    ///     NOT Thread-safe
    /// </summary>
    public class EntityParticipantTable : IEntityParticipantTable
    {
        private readonly Dictionary<string, Entity> walletIdToEntity = new ();
        private readonly Dictionary<Entity, string> entityToWalletId = new ();

        public int Count => walletIdToEntity.Count;

        public Entity Entity(string walletId)
        {
            try { return walletIdToEntity[walletId]; }
            catch (Exception e) { throw new Exception($"Cannot find entity for walletId: {walletId}", e); }
        }

        public string WalletId(Entity entity) =>
            entityToWalletId[entity].EnsureNotNull();

        public bool Has(string walletId) =>
            walletIdToEntity.ContainsKey(walletId);

        public void Register(string walletId, Entity entity)
        {
            walletIdToEntity.Add(walletId, entity);
            entityToWalletId.Add(entity, walletId);
        }

        public void Release(string walletId)
        {
            Entity entity = walletIdToEntity[walletId];
            walletIdToEntity.Remove(walletId);
            entityToWalletId.Remove(entity);
        }
    }
}
