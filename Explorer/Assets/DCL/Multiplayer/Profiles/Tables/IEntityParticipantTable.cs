using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;

namespace DCL.Multiplayer.Profiles.Tables
{
    public interface IEntityParticipantTable : IReadOnlyEntityParticipantTable
    {
        void Register(string walletId, Entity entity, RoomSource fromRoom);

        void AddRoomSource(string walletId, RoomSource fromRoom);

        /// <summary>
        ///     Returns true if the entity is no longer connected to any room
        /// </summary>
        bool Release(string walletId, RoomSource fromRoom);
    }
}
