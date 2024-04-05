using Arch.Core;
using CommunicationData.URLHelpers;

namespace DCL.Multiplayer.Emotes.Interfaces
{
    public interface IEmotesMessageBus
    {
        void InjectWorld(World world, Entity playerEntity);

        void Send(URN urn, bool loopCyclePassed, bool sendToSelfReplica);

        void OnPlayerRemoved(string walletId);
    }
}
