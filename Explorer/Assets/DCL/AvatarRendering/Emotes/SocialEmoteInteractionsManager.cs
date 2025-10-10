using CodeLess.Attributes;
using DCL.AvatarRendering.Emotes;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SocialEmotes
{
    [Singleton]
    public partial class SocialEmoteInteractionsManager
    {
        public delegate void InteractionDelegate(SocialEmoteInteractionReadOnly interaction);
        public delegate void ParticipantAddedDelegate(string participantWalletAddress, SocialEmoteInteractionReadOnly interaction);

        // TODO replace this with an interface with gets
        public struct SocialEmoteInteractionReadOnly
        {
            private SocialEmoteInteraction original;

            public SocialEmoteInteractionReadOnly(SocialEmoteInteraction original)
            {
                this.original = original;
            }

            public string InitiatorWalletAddress => original.InitiatorWalletAddress;
            public string ReceiverWalletAddress => original.ReceiverWalletAddress;
            public IEmote Emote => original.Emote;
            public bool AreInteracting => original.AreInteracting;
            public int OutcomeIndex => original.OutcomeIndex;
            public Vector3 InitiatorPosition => original.InitiatorPosition;
            public Quaternion InitiatorRotation => original.InitiatorRotation;
        }

        public class SocialEmoteInteraction
        {
            public string InitiatorWalletAddress;
            public string ReceiverWalletAddress;
            public IEmote Emote;
            public bool AreInteracting;
            public int OutcomeIndex;
            public Vector3 InitiatorPosition;
            public Quaternion InitiatorRotation;
        }

        public event InteractionDelegate InteractionStarted;
        public event InteractionDelegate InteractionStopped;
        public event ParticipantAddedDelegate ParticipantAdded;

        private readonly Dictionary<string, SocialEmoteInteraction> participantInteractions = new Dictionary<string, SocialEmoteInteraction>();

        public void StartInteraction(string initiatorWalletAddress, IEmote emote, Transform initiatorTransform)
        {
            Debug.LogError("START INTERACTION " + initiatorWalletAddress);

            if (participantInteractions.ContainsKey(initiatorWalletAddress))
                return;

            // TODO: Use a pool
            SocialEmoteInteraction newInteraction = new SocialEmoteInteraction()
            {
                InitiatorWalletAddress = initiatorWalletAddress,
                Emote = emote,
                InitiatorPosition = initiatorTransform.position,
                InitiatorRotation = initiatorTransform.rotation
            };

            participantInteractions.Add(initiatorWalletAddress, newInteraction);
            InteractionStarted?.Invoke(new SocialEmoteInteractionReadOnly(newInteraction));
        }

        public void AddParticipantToInteraction(string participantWalletAddress, int outcomeIndex, string initiatorWalletAddress)
        {
            Debug.LogError("Add to Interaction " + participantWalletAddress);

            if (participantInteractions.ContainsKey(participantWalletAddress))
                return;

            SocialEmoteInteraction interaction = participantInteractions[initiatorWalletAddress];
            interaction.AreInteracting = true;
            Debug.LogError("AreInteracting = true");
            interaction.ReceiverWalletAddress = participantWalletAddress;
            interaction.OutcomeIndex = outcomeIndex;
            participantInteractions.Add(participantWalletAddress, interaction);
            ParticipantAdded?.Invoke(participantWalletAddress, new SocialEmoteInteractionReadOnly(interaction));
        }

        public void StopInteraction(string participantWalletAddress)
        {
            if(string.IsNullOrEmpty(participantWalletAddress))
                return;

            if (!participantInteractions.ContainsKey(participantWalletAddress))
                return;

            Debug.LogError("StopInteraction " + participantWalletAddress);

            SocialEmoteInteraction interaction = participantInteractions[participantWalletAddress];
            participantInteractions.Remove(interaction.InitiatorWalletAddress);

            if(!string.IsNullOrEmpty(interaction.ReceiverWalletAddress))
                participantInteractions.Remove(interaction.ReceiverWalletAddress);

            InteractionStopped?.Invoke(new SocialEmoteInteractionReadOnly(interaction));
        }

        public SocialEmoteInteractionReadOnly? GetInteractionState(string participantWalletAddress)
        {
            if (participantInteractions.TryGetValue(participantWalletAddress, out SocialEmoteInteraction interaction))
                return new SocialEmoteInteractionReadOnly(interaction);
            else
                return null;
        }
    }
}
