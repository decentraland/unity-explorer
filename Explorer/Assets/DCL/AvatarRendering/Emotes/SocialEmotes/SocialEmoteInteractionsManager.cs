
using CodeLess.Attributes;
using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
{
    /// <summary>
    /// Stores the state of all social emote interactions. An interaction only exists if somebody initiated it.
    /// </summary>
    [Singleton]
    public partial class SocialEmoteInteractionsManager
    {
        public delegate void InteractionDelegate(ISocialEmoteInteractionReadOnly? interaction);
        public delegate void ParticipantAddedDelegate(string participantWalletAddress, ISocialEmoteInteractionReadOnly? interaction);

        public interface ISocialEmoteInteractionReadOnly
        {
            string InitiatorWalletAddress { get; }
            string ReceiverWalletAddress { get; }
            IEmote Emote { get; }
            bool AreInteracting { get; }
            int OutcomeIndex { get; }
            Vector3 InitiatorPosition { get; }
            Quaternion InitiatorRotation { get; }
        }

        /// <summary>
        /// Stores the current state of an interaction.
        /// </summary>
        private class SocialEmoteInteraction : ISocialEmoteInteractionReadOnly
        {
            public string InitiatorWalletAddress { get; set; }
            public string ReceiverWalletAddress { get; set; }
            public IEmote? Emote { get; set; }
            public bool AreInteracting { get; set; }
            public int OutcomeIndex { get; set; }
            public Vector3 InitiatorPosition { get; set; }
            public Quaternion InitiatorRotation { get; set; }

            public void Reset()
            {
                InitiatorWalletAddress = string.Empty;
                ReceiverWalletAddress = string.Empty;
                Emote = null;
                AreInteracting = false;
                OutcomeIndex = -1;
                InitiatorPosition = Vector3.zero;
                InitiatorRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Raised when an avatar played the start animation of a social emote.
        /// </summary>
        public event InteractionDelegate InteractionStarted;

        /// <summary>
        /// Raised when an avatar canceled or finished the animation.
        /// </summary>
        public event InteractionDelegate InteractionStopped;

        /// <summary>
        /// Raised when an avatar reacted to an interaction.
        /// </summary>
        public event ParticipantAddedDelegate ParticipantAdded;

        private readonly Dictionary<string, SocialEmoteInteraction?> participantInteractions = new Dictionary<string, SocialEmoteInteraction?>();

        private readonly IObjectPool<SocialEmoteInteraction?> interactionPool = new ObjectPool<SocialEmoteInteraction?>(createFunc: () => { return new SocialEmoteInteraction(); });

        /// <summary>
        /// Registers a new interaction. Called when an avatar plays the start animation of a social emote.
        /// </summary>
        /// <param name="initiatorWalletAddress">The wallet address of the player that initiated the interaction.</param>
        /// <param name="emote">The social emote played by the initiator.</param>
        /// <param name="initiatorTransform">The transform component of the initiator.</param>
        public void StartInteraction(string initiatorWalletAddress, IEmote emote, Transform initiatorTransform)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "START INTERACTION " + initiatorWalletAddress);

            if (participantInteractions.ContainsKey(initiatorWalletAddress))
                return;

            SocialEmoteInteraction? newInteraction = interactionPool.Get();
            newInteraction!.Reset();
            newInteraction.InitiatorWalletAddress = initiatorWalletAddress;
            newInteraction.Emote = emote;
            newInteraction.InitiatorPosition = initiatorTransform.position;
            newInteraction.InitiatorRotation = initiatorTransform.rotation;

            participantInteractions.Add(initiatorWalletAddress, newInteraction);
            InteractionStarted?.Invoke(newInteraction);
        }

        /// <summary>
        /// Stores the avatar that has reacted to the social emote interaction initiated by other.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet address of the player that reacted.</param>
        /// <param name="outcomeIndex">The index, starting at zero, of the outcome animation chosen by the reacting player.</param>
        /// <param name="initiatorWalletAddress">The wallet addres of the player whose interaction the new participant is reacting to.</param>
        public void AddParticipantToInteraction(string participantWalletAddress, int outcomeIndex, string initiatorWalletAddress)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "Add to Interaction " + participantWalletAddress);

            if (participantInteractions.ContainsKey(participantWalletAddress))
                return;

            SocialEmoteInteraction? interaction = participantInteractions[initiatorWalletAddress];
            interaction!.AreInteracting = true;
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AreInteracting = true");
            interaction.ReceiverWalletAddress = participantWalletAddress;
            interaction.OutcomeIndex = outcomeIndex;
            participantInteractions.Add(participantWalletAddress, interaction);
            ParticipantAdded?.Invoke(participantWalletAddress, interaction);
        }

        /// <summary>
        /// Unregisters an interaction by providing one of the involved participants.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet addres of one of the players that participated in the interaction.</param>
        public void StopInteraction(string participantWalletAddress)
        {
            if(string.IsNullOrEmpty(participantWalletAddress))
                return;

            if (!participantInteractions.ContainsKey(participantWalletAddress))
                return;

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "StopInteraction " + participantWalletAddress);

            SocialEmoteInteraction? interaction = participantInteractions[participantWalletAddress];
            participantInteractions.Remove(interaction!.InitiatorWalletAddress);

            if(!string.IsNullOrEmpty(interaction.ReceiverWalletAddress))
                participantInteractions.Remove(interaction.ReceiverWalletAddress);

            interactionPool.Release(interaction);

            InteractionStopped?.Invoke(interaction);
        }

        /// <summary>
        /// Obtains the current state of an interaction by providing one of the players involved in it.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet addres of one of the participants.</param>
        /// <returns>The current state, or null if the player is not interacting.</returns>
        public ISocialEmoteInteractionReadOnly? GetInteractionState(string participantWalletAddress) =>
            participantInteractions.GetValueOrDefault(participantWalletAddress);
    }
}
