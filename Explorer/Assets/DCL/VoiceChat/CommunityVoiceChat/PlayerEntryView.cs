using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class PlayerEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal const float ANIMATION_DURATION = 0.5f;
        public event Action<VoiceChatParticipantsStateService.ParticipantState, VoiceChatParticipantsStateService.ParticipantState, Vector2, PlayerEntryView>? ContextMenuButtonClicked;

        [SerializeField] private RectTransform hoverElement;

        [SerializeField] private Button contextMenuButton;

        [SerializeField] public ProfilePictureView ProfilePictureView;
        [SerializeField] public SimpleUserNameElement nameElement;
        [field: SerializeField]
        internal RectTransform isSpeakingIcon { get; private set; }

        [field: SerializeField]
        internal Image isSpeakingIconRenderer { get; private set; }

        [field: SerializeField]
        internal Image isSpeakingIconOuterRenderer { get; private set; }

        [field: SerializeField]
        internal RectTransform isSpeakingIconRect { get; private set; }

        [field: SerializeField]
        internal RectTransform isSpeakingIconOuterRect { get; private set; }

        [field: SerializeField]
        internal GameObject approveDenySection { get; private set; }

        [field: SerializeField]
        internal Button approveButton { get; private set; }

        [field: SerializeField]
        internal Button denyButton { get; private set; }

        private VoiceChatParticipantsStateService.ParticipantState userProfile;
        private VoiceChatParticipantsStateService.ParticipantState localUserProfile;
        private Sequence isSpeakingCurrentSequence;

        public event Action<string> ApproveSpeaker;
        public event Action<string> DenySpeaker;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            isSpeakingIcon.gameObject.SetActive(false);
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
            isSpeakingIcon.gameObject.SetActive(isSpeaking);
            if (isSpeaking)
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(isSpeakingIconRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconOuterRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Append(isSpeakingIconOuterRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.SetLoops(-1);
                isSpeakingCurrentSequence.Play();
            }
            else
            {
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
            }
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
