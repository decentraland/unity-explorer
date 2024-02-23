using Arch.Core;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IEntityParticipantTable
    {
        Entity Entity(string walletId);

        string WalletId(Entity entity);

        bool Has(string walletId);

        void Register(string walletId, Entity entity);

        void Release(string walletId);
    }
}
