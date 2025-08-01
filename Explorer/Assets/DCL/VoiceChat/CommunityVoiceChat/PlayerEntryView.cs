using DCL.UI.ProfileElements;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class PlayerEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<VoiceChatParticipantsStateService.ParticipantState, VoiceChatParticipantsStateService.ParticipantState, Vector2, PlayerEntryView>? ContextMenuButtonClicked;

        [SerializeField] private RectTransform hoverElement;

        [SerializeField] private Button contextMenuButton;

        [SerializeField] public ProfilePictureView ProfilePictureView;
        [SerializeField] public SimpleUserNameElement nameElement;

        private VoiceChatParticipantsStateService.ParticipantState userProfile;
        private VoiceChatParticipantsStateService.ParticipantState localUserProfile;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(userProfile!, localUserProfile!, contextMenuButton.transform.position, this));
        }

        public void SetUserProfile(VoiceChatParticipantsStateService.ParticipantState participantState, VoiceChatParticipantsStateService.ParticipantState localParticipantState)
        {
            userProfile = participantState;
            localUserProfile = localParticipantState;
            userProfile.IsSpeaking.OnUpdate -= OnChangeIsSpeaking;
            userProfile.IsSpeaking.OnUpdate += OnChangeIsSpeaking;
        }

        private void OnChangeIsSpeaking(bool isSpeaking)
        {
            //Handle is speaking logic and visuals
        }

        public void SubscribeToInteractions(Action<VoiceChatParticipantsStateService.ParticipantState, VoiceChatParticipantsStateService.ParticipantState, Vector2, PlayerEntryView> contextMenu)
        {
            RemoveAllListeners();

            ContextMenuButtonClicked += contextMenu;
        }

        private void RemoveAllListeners()
        {
            ContextMenuButtonClicked = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(false);
        }
    }
}
