using Arch.Core;
using Cysharp.Threading.Tasks;
using Decentraland.Kernel.Comms.Rfc4;

namespace DCL.Multiplayer.Emotes.Interfaces
{
    public interface IEmotesMessageBus
    {
        void InjectWorld(World world);

        void Send(uint emoteIndex);

        UniTaskVoid SelfSendWithDelayAsync(Emote emote, float f);
    }
}
