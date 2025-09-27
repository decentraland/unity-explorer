
using CodeLess.Attributes;
using System.Collections.Generic;

namespace DCL.AvatarRendering.Emotes.SocialEmotes
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
        }

        public class SocialEmoteInteraction
        {
            public string InitiatorWalletAddress;
            public string ReceiverWalletAddress;
            public IEmote Emote;
            public bool AreInteracting;
            public int OutcomeIndex;
        }

        public event InteractionDelegate InteractionStarted;
        public event InteractionDelegate InteractionStopped;
        public event ParticipantAddedDelegate ParticipantAdded;

        private readonly Dictionary<string, SocialEmoteInteraction> participantInteractions = new Dictionary<string, SocialEmoteInteraction>();

        public void StartInteraction(string initiatorWalletAddress, IEmote emote)
        {
            SocialEmoteInteraction newInteraction = new SocialEmoteInteraction()
            {
                InitiatorWalletAddress = initiatorWalletAddress,
                Emote = emote
            };

            participantInteractions.Add(initiatorWalletAddress, newInteraction);
            InteractionStarted?.Invoke(new SocialEmoteInteractionReadOnly(newInteraction));
        }

        public void AddParticipantToInteraction(string participantWalletAddress, int outcomeIndex, string initiatorWalletAddress)
        {
            SocialEmoteInteraction interaction = participantInteractions[initiatorWalletAddress];
            interaction.AreInteracting = true;
            interaction.ReceiverWalletAddress = participantWalletAddress;
            interaction.OutcomeIndex = outcomeIndex;
            participantInteractions.Add(participantWalletAddress, interaction);
            ParticipantAdded?.Invoke(participantWalletAddress, new SocialEmoteInteractionReadOnly(interaction));
        }

        public void StopInteraction(string participantWalletAddress)
        {
            SocialEmoteInteraction interaction = participantInteractions[participantWalletAddress];
            participantInteractions.Remove(interaction.InitiatorWalletAddress);

            if(!string.IsNullOrEmpty(interaction.ReceiverWalletAddress))
                participantInteractions.Remove(interaction.ReceiverWalletAddress);

            InteractionStopped?.Invoke(new SocialEmoteInteractionReadOnly(interaction));
        }

        public SocialEmoteInteractionReadOnly? GetInteractionState(string participantWalletAddress)
        {
            if (participantInteractions.TryGetValue(participantWalletAddress, out SocialEmoteInteraction interaction))
            {
                return new SocialEmoteInteractionReadOnly(interaction);
            }
            else
            {
                return null;
            }
        }

    }
}
