using Arch.Core;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IReadOnlyEntityParticipantTable
    {
        int Count { get; }

        Entity Entity(string walletId);

        string WalletId(Entity entity);

        bool Has(string walletId);

        IReadOnlyCollection<string> Wallets();
    }
}
