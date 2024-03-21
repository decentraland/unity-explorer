using Arch.Core;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IReadOnlyEntityParticipantTable
    {
        Entity Entity(string walletId);

        string WalletId(Entity entity);

        bool Has(string walletId);
    }
}
