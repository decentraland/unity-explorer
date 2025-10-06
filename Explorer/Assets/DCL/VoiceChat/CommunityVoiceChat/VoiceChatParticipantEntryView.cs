using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI.ProfileElements;
using DG.Tweening;
using MVC;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class VoiceChatParticipantEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float ANIMATION_DURATION = 0.5f;
        private static readonly Vector3 IDLE_SCALE = new (1, 0.2f, 1);
        private const string IS_MUTED_TEXT = "Muted";
        private const string IS_SPEAKING_TEXT = "Speaking";

        public event Action<VoiceChatParticipantState, Vector2, VoiceChatParticipantEntryView>? ContextMenuButtonClicked;
        public event Action<string>? ApproveSpeaker;
        public event Action<string>? DenySpeaker;

        [field: SerializeField] public ProfilePictureView ProfilePictureView { get; private set; } = null!;
        [field: SerializeField] public TMP_Text NameElement { get; private set; } = null!;
        [field: SerializeField] public RectTransform IsSpeakingIcon { get; private set; } = null!;

        [SerializeField] private RectTransform isSpeakingIconRect = null!;
        [SerializeField] private RectTransform isSpeakingIconOuterRect = null!;
        [SerializeField] private GameObject approveDenySection  = null!;
        [SerializeField] private Button approveButton = null!;
        [SerializeField] private Button denyButton = null!;
        [SerializeField] private Button openPassportButton  = null!;
        [SerializeField] private RectTransform hoverElement = null!;
        [SerializeField] private Button contextMenuButton = null!;
        [SerializeField] private GameObject isMutedIcon = null!;
        [SerializeField] private TMP_Text statusText = null!;
        [SerializeField] private GameObject promotingSpinner = null!;

        private VoiceChatParticipantState? userProfile;
        private VoiceChatParticipantState? localUserProfile;
        private Sequence? isSpeakingCurrentSequence;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(OnOpenContextMenuClicked);
            openPassportButton.onClick.AddListener(OnOpenPassportClicked);
        }

        private void OnOpenContextMenuClicked()
        {
            ContextMenuButtonClicked?.Invoke(userProfile!, contextMenuButton.transform.position, this);
        }

        public void CleanupEntry()
        {
            isSpeakingCurrentSequence?.Kill();
            isSpeakingCurrentSequence = null;
            SetSpeakingIconIdleScale();
            approveDenySection.SetActive(false);
            IsSpeakingIcon.gameObject.SetActive(false);
            isMutedIcon.SetActive(false);
        }

        private void OnOpenPassportClicked()
        {
            if (string.IsNullOrEmpty(userProfile?.WalletId)) return;

            OpenPassportAsync(userProfile.WalletId, CancellationToken.None).Forget();
            return;

            async UniTask OpenPassportAsync(string userId, CancellationToken ct = default)
            {
                try
                {
                    await ViewDependencies.GlobalUIViews.OpenPassportAsync(userId, ct);
                }
                catch (Exception ex)
                {
                    ReportHub.LogError(ReportCategory.COMMUNITY_VOICE_CHAT, $"Failed to open passport for user {userId}: {ex.Message}");
                }
            }
        }


        public void SetUserProfile(VoiceChatParticipantState participantState, VoiceChatParticipantState localParticipantState)
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

            userProfile.IsMuted.OnUpdate -= OnIsMutedChanged;
            userProfile.IsMuted.OnUpdate += OnIsMutedChanged;

            approveButton.onClick.RemoveAllListeners();
            approveButton.onClick.AddListener(OnApproveButtonClicked);
            denyButton.onClick.RemoveAllListeners();
            denyButton.onClick.AddListener(OnDenyButtonClicked);

            SetSpeakingIconIdleScale();
        }

        private void OnDenyButtonClicked()
        {
            if (userProfile != null)
                DenySpeaker?.Invoke(userProfile.WalletId);
        }

        private void OnApproveButtonClicked()
        {
            if (userProfile != null)
                ApproveSpeaker?.Invoke(userProfile.WalletId);
        }

        private void OnIsMutedChanged(bool isMuted)
        {
            if (!userProfile!.IsSpeaker.Value) return;

            if (isMuted)
            {
                statusText.text = IS_MUTED_TEXT;
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
                IsSpeakingIcon.gameObject.SetActive(false);
                isMutedIcon.SetActive(true);
            }
            else if (userProfile.IsSpeaker.Value)
            {
                statusText.text = IS_SPEAKING_TEXT;
                IsSpeakingIcon.gameObject.SetActive(true);
                isMutedIcon.SetActive(false);
                SetSpeakingIconIdleScale();
            }
        }

        private void SetRequestingToSpeakSection(bool isRequestingToSpeak) =>
            approveDenySection.SetActive(isRequestingToSpeak && localUserProfile?.Role.Value is
                VoiceChatParticipantsStateService.UserCommunityRoleMetadata.moderator or
                VoiceChatParticipantsStateService.UserCommunityRoleMetadata.owner);

        private void OnChangeIsSpeaking(bool isSpeaking)
        {
            if (userProfile!.IsMuted.Value) return;

            isMutedIcon.SetActive(false);

            statusText.text = IS_SPEAKING_TEXT;

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

        public void SubscribeToInteractions(Action<VoiceChatParticipantState, Vector2, VoiceChatParticipantEntryView> contextMenu, Action<string> approveSpeaker, Action<string> denySpeaker)
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

        public void ConfigureAsSpeaker()
        {
            IsSpeakingIcon.gameObject.SetActive(true);
            statusText.text = IS_SPEAKING_TEXT;
            isMutedIcon.SetActive(false);
            promotingSpinner.SetActive(false);
        }

        public void ConfigureAsListener()
        {
            statusText.text = string.Empty;
            IsSpeakingIcon.gameObject.SetActive(false);
            isMutedIcon.SetActive(false);
            transform.localScale = Vector3.one;
            promotingSpinner.SetActive(false);
        }
    }
}
