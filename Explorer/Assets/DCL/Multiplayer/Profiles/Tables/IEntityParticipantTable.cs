using Arch.Core;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IEntityParticipantTable : IReadOnlyEntityParticipantTable
    {
        void Register(string walletId, Entity entity);

        void Release(string walletId);
    }
}
