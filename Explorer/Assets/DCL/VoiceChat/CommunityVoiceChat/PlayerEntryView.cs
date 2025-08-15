using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class PlayerEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float ANIMATION_DURATION = 0.5f;
        private static readonly Vector3 IDLE_SCALE = new (1, 0.2f, 1);

        public event Action<VoiceChatParticipantsStateService.ParticipantState, Vector2, PlayerEntryView>? ContextMenuButtonClicked;

        [SerializeField] private RectTransform hoverElement;

        [SerializeField] private Button contextMenuButton;

        [SerializeField] public ProfilePictureView ProfilePictureView;
        [SerializeField] public TMP_Text nameElement;

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
            contextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(userProfile!, contextMenuButton.transform.position, this));
        }

        public void SetUserProfile(VoiceChatParticipantsStateService.ParticipantState participantState, VoiceChatParticipantsStateService.ParticipantState localParticipantState)
        {
            // We only show context menu button on top of local participant if local participant is a mod.
            var showContextMenuButton = true;

            if (participantState.Name.Value == localParticipantState.Name.Value)
                showContextMenuButton = localParticipantState.Role.Value is VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner;

            contextMenuButton.gameObject.SetActive(showContextMenuButton);

            userProfile = participantState;
            localUserProfile = localParticipantState;

            userProfile.IsSpeaking.OnUpdate -= OnChangeIsSpeaking;
            userProfile.IsSpeaking.OnUpdate += OnChangeIsSpeaking;

            userProfile.IsRequestingToSpeak.OnUpdate -= SetRequestingToSpeakSection;
            userProfile.IsRequestingToSpeak.OnUpdate += SetRequestingToSpeakSection;

            approveButton.onClick.RemoveAllListeners();
            approveButton.onClick.AddListener(() => ApproveSpeaker?.Invoke(userProfile.WalletId));
            denyButton.onClick.RemoveAllListeners();
            denyButton.onClick.AddListener(() => DenySpeaker?.Invoke(userProfile.WalletId));

            SetSpeakingIconIdleScale();
        }

        private void SetRequestingToSpeakSection(bool isRequestingToSpeak) =>
            approveDenySection.SetActive(isRequestingToSpeak && localUserProfile.Role.Value is
                VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or
                VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner);

        private void OnChangeIsSpeaking(bool isSpeaking)
        {
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
                SetSpeakingIconIdleScale();
            }
        }

        private void SetSpeakingIconIdleScale()
        {
            isSpeakingIconRect.localScale = IDLE_SCALE;
            isSpeakingIconOuterRect.localScale = IDLE_SCALE;
        }

        public void SubscribeToInteractions(Action<VoiceChatParticipantsStateService.ParticipantState, Vector2, PlayerEntryView> contextMenu, Action<string> approveSpeaker, Action<string> denySpeaker)
        {
            RemoveAllListeners();

            ContextMenuButtonClicked += contextMenu;
            ApproveSpeaker += approveSpeaker;
            DenySpeaker += denySpeaker;
        }

        private void RemoveAllListeners()
        {
            ContextMenuButtonClicked = null;
            ApproveSpeaker = null;
            DenySpeaker = null;
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
