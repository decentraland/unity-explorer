using Arch.Core;
using CodeLess.Attributes;
using DCL.Diagnostics;
using DCL.AvatarRendering.Emotes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.SocialEmotes
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
            Entity InitiatorEntity { get; }
            Entity ReceiverEntity { get; }
            IEmote Emote { get; }
            bool AreInteracting { get; }
            int OutcomeIndex { get; }
            Vector3 InitiatorPosition { get; }
            Quaternion InitiatorRotation { get; }
            string TargetWalletAddress { get; }
            int Id { get; }
        }

        /// <summary>
        /// Stores the current state of an interaction.
        /// </summary>
        private class SocialEmoteInteraction : ISocialEmoteInteractionReadOnly
        {
            public string InitiatorWalletAddress { get; set; }
            public string ReceiverWalletAddress { get; set; }
            public Entity InitiatorEntity { get; set; }
            public Entity ReceiverEntity { get; set; }
            public IEmote? Emote { get; set; }
            public bool AreInteracting { get; set; }
            public int OutcomeIndex { get; set; }
            public Vector3 InitiatorPosition { get; set; }
            public Quaternion InitiatorRotation { get; set; }
            public string TargetWalletAddress { get; set; }
            public int Id { get; set; }

            public void Reset()
            {
                InitiatorWalletAddress = string.Empty;
                ReceiverWalletAddress = string.Empty;
                InitiatorEntity = Entity.Null;
                ReceiverEntity = Entity.Null;
                Emote = null;
                AreInteracting = false;
                OutcomeIndex = -1;
                InitiatorPosition = Vector3.zero;
                InitiatorRotation = Quaternion.identity;
                TargetWalletAddress = string.Empty;
                Id = 0;
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

        private readonly HashSet<int> everExistingInteractions = new ();

        /// <summary>
        /// Registers a new interaction. Called when an avatar plays the start animation of a social emote.
        /// </summary>
        /// <param name="initiatorWalletAddress">The wallet address of the player that initiated the interaction.</param>
        /// <param name="initiatorEntity">The ECS id of the avatar that initiated the interaction.</param>
        /// <param name="emote">The social emote played by the initiator.</param>
        /// <param name="initiatorTransform">The transform component of the initiator.</param>
        /// <param name="interactionId">A unique ID for the interaction.</param>
        /// <param name="targetWalletAddress">Optional. The wallet address of the player whom the emote is directed. Only that player can be added to the interaction.</param>
        public void StartInteraction(string initiatorWalletAddress, Entity initiatorEntity, IEmote emote, Transform initiatorTransform, int interactionId, string targetWalletAddress = "")
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=yellow>START INTERACTION " + initiatorWalletAddress + " id: " + interactionId + " target: " + targetWalletAddress + "</color>");

            if (participantInteractions.ContainsKey(initiatorWalletAddress))
                return;

            SocialEmoteInteraction? newInteraction = interactionPool.Get();
            newInteraction!.Reset();
            newInteraction.InitiatorWalletAddress = initiatorWalletAddress;
            newInteraction.InitiatorEntity = initiatorEntity;
            newInteraction.Emote = emote;
            newInteraction.InitiatorPosition = initiatorTransform.position;
            newInteraction.InitiatorRotation = initiatorTransform.rotation;
            newInteraction.TargetWalletAddress = targetWalletAddress;
            newInteraction.Id = interactionId;

            participantInteractions.Add(initiatorWalletAddress, newInteraction);
            everExistingInteractions.Add(interactionId);
            InteractionStarted?.Invoke(newInteraction);
        }

        /// <summary>
        /// Stores the avatar that has reacted to the social emote interaction initiated by others.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet address of the player that reacted.</param>
        /// <param name="participantEntity">The ECS id of the avatar that reacted.</param>
        /// <param name="outcomeIndex">The index, starting at zero, of the outcome animation chosen by the reacting player.</param>
        /// <param name="initiatorWalletAddress">The wallet address of the player whose interaction the new participant is reacting to.</param>
        public void AddParticipantToInteraction(string participantWalletAddress, Entity participantEntity, int outcomeIndex, string initiatorWalletAddress)
        {
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=yellow>Add to Interaction " + participantWalletAddress + "</color>");

            if (participantInteractions.ContainsKey(participantWalletAddress))
                return;

            SocialEmoteInteraction? interaction = participantInteractions[initiatorWalletAddress];
            interaction!.AreInteracting = true;
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "AreInteracting = true, id = " + interaction.Id);
            interaction.ReceiverWalletAddress = participantWalletAddress;
            interaction.ReceiverEntity = participantEntity;
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
            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "<color=yellow>StopInteraction " + participantWalletAddress + "</color>");

            if(string.IsNullOrEmpty(participantWalletAddress))
                return;

            if (!participantInteractions.ContainsKey(participantWalletAddress))
                return;

            SocialEmoteInteraction? interaction = participantInteractions[participantWalletAddress];

            ReportHub.LogError(ReportCategory.EMOTE_DEBUG, "interaction Id: " + interaction.Id);

            participantInteractions.Remove(interaction!.InitiatorWalletAddress);

            if(!string.IsNullOrEmpty(interaction.ReceiverWalletAddress))
                participantInteractions.Remove(interaction.ReceiverWalletAddress);

            interactionPool.Release(interaction);

            InteractionStopped?.Invoke(interaction);
        }

        /// <summary>
        /// Checks whether an interaction started.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet address of one of the participants in the interaction.</param>
        /// <returns>True if the interaction exists and can be retrieved; False otherwise.</returns>
        public bool InteractionExists(string participantWalletAddress) =>
            participantInteractions.ContainsKey(participantWalletAddress);

        /// <summary>
        /// Obtains the current state of an interaction by providing one of the players involved in it.
        /// </summary>
        /// <param name="participantWalletAddress">The wallet addres of one of the participants.</param>
        /// <returns>The current state, or null if the player is not interacting.</returns>
        public ISocialEmoteInteractionReadOnly? GetInteractionState(string participantWalletAddress) =>
            participantInteractions.GetValueOrDefault(participantWalletAddress);

        /// <summary>
        /// Checks if an interaction has ever existed in the current client, by looking for its ID.
        /// </summary>
        /// <remarks>
        /// This is useful when a client connects while interactions are already occurring, so it is possible to distinguish which occurred while connected and which did before connecting.
        /// </remarks>
        /// <param name="interactionId">The ID to be checked.</param>
        /// <returns>True if the interaction existed; False otherwise.</returns>
        public bool HasInteractionExisted(int interactionId) =>
            everExistingInteractions.Contains(interactionId);
    }
}
