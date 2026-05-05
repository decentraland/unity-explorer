using System.Collections.Generic;

namespace DCL.Multiplayer.FacialExpression
{
    public interface IFacialExpressionMessageBus
    {
        /// <summary>Edge-triggered send of the local player's expression indices to remote peers.</summary>
        void Send(byte eyebrowsIndex, byte eyesIndex, byte mouthIndex);

        /// <summary>Moves all received intentions into <paramref name="output"/> and clears the internal buffer.</summary>
        void Drain(ICollection<RemoteFacialExpressionIntention> output);

        /// <summary>Re-queues an intention whose target entity wasn't ready this frame.</summary>
        void SaveForRetry(RemoteFacialExpressionIntention intention);
    }
}