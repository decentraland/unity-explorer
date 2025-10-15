using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.SocialEmotes
{
    public class DebugSocialEmoteInteractionsView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text initiatorText;

        [SerializeField]
        private TMP_Text receiverText;

        [SerializeField]
        private TMP_Text emoteText;

        [SerializeField]
        private TMP_Text outcomeText;

        [SerializeField]
        private Toggle areInteractingToggle;

        public void SetInitiatorWalletAddress(string wallet)
        {
            initiatorText.text = wallet;
        }

        public void SetReceiverWalletAddress(string wallet)
        {
            receiverText.text = wallet;
        }

        public void SetEmoteUrn(string emote)
        {
            emoteText.text = emote;
        }

        public void SetOutcomeIndex(int outcomeIndex)
        {
            outcomeText.text = outcomeIndex.ToString();
        }

        public void SetAreInteracting(bool areInteracting)
        {
            areInteractingToggle.isOn = areInteracting;
        }

        public void OnInteractionStarted(SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly interaction)
        {
            SetInitiatorWalletAddress(interaction.InitiatorWalletAddress);
            SetEmoteUrn(interaction.Emote.Model!.Asset!.id!);
            SetReceiverWalletAddress("-1");
            SetOutcomeIndex(-1);
            SetAreInteracting(false);
        }

        public void OnInteractionStopped(SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly interaction)
        {
            SetInitiatorWalletAddress("STOPPED " + interaction.InitiatorWalletAddress);
            SetInitiatorWalletAddress("STOPPED " + interaction.ReceiverWalletAddress);
            SetAreInteracting(interaction.AreInteracting);
        }

        public void OnParticipantAdded(string participantWalletAddress, SocialEmoteInteractionsManager.ISocialEmoteInteractionReadOnly interaction)
        {
            SetInitiatorWalletAddress(interaction.InitiatorWalletAddress);
            SetEmoteUrn(interaction.Emote.Model!.Asset!.id!);
            SetReceiverWalletAddress(participantWalletAddress);
            SetOutcomeIndex(interaction.OutcomeIndex);
            SetAreInteracting(interaction.AreInteracting);
        }
    }
}
