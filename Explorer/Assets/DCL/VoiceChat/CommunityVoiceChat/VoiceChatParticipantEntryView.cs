using Cysharp.Threading.Tasks;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles.Helpers;
using DCL.Utilities;
using DG.Tweening;
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
        private const string IS_MUTED_TEXT = "Muted";
        private const string IS_SPEAKING_TEXT = "Speaking";
        private static readonly Vector3 IDLE_SCALE = new (1, 0.2f, 1);

        public event Action? ApproveSpeaker;
        public event Action? DenySpeaker;

        public event Action<Vector2>? OpenContextMenu;
        public event Action? OpenPassport;

        [Header("Profile Elements")]
        [SerializeField] private ProfilePictureView profilePictureView = null!;
        [SerializeField] private TMP_Text nameElement = null!;
        [Header("State Section")]
        [SerializeField] private RectTransform isSpeakingIcon = null!;
        [SerializeField] private RectTransform isSpeakingIconRect = null!;
        [SerializeField] private RectTransform isSpeakingIconOuterRect = null!;
        [SerializeField] private GameObject isMutedIcon = null!;
        [SerializeField] private TMP_Text stateText = null!;
        [SerializeField] private GameObject stateSection = null!;
        [Header("Approve-Deny Section")]
        [SerializeField] private GameObject approveDenySection = null!;
        [SerializeField] private Button approveButton = null!;
        [SerializeField] private Button denyButton = null!;
        [Header("Buttons And Interactions")]
        [SerializeField] private Button openPassportButton = null!;
        [SerializeField] private RectTransform hoverElement = null!;
        [SerializeField] private Button contextMenuButton = null!;
        [SerializeField] private GameObject promotingSpinner = null!;

        private Sequence? isSpeakingCurrentSequence;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(OnOpenContextMenuClicked);
            openPassportButton.onClick.AddListener(OnOpenPassportClicked);
            approveButton.onClick.AddListener(OnApproveButtonClicked);
            denyButton.onClick.AddListener(OnDenyButtonClicked);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(false);
        }

        public void CleanupEntry()
        {
            SetSpeakingIconIdleScale();
            approveDenySection.SetActive(false);
            isSpeakingIcon.gameObject.SetActive(false);
            isMutedIcon.SetActive(false);
            promotingSpinner.SetActive(false);
        }

        public void ConfigureInitialState(bool isSpeaker, bool isMuted)
        {
            OnIsSpeakerChanged(isSpeaker, isMuted);
        }

        public void OnIsMutedChanged(bool isMuted)
        {
            if (isMuted)
                SetAsMuted();
            else
                SetAsSpeaker();
        }

        public void OnIsSpeakerChanged(bool isSpeaker, bool isMuted)
        {
            switch (isSpeaker)
            {
                case false:
                    SetAsListener(); break;
                case true when isMuted:
                    SetAsMuted(); break;
                case true:
                    SetAsSpeaker(); break;
            }
        }

        public void OnIsSpeakingChanged(bool isSpeaking)
        {
            if (isSpeaking)
                SetAsIsSpeaking();
            else
                SetAsSpeaker();
        }

        public void ParticipantRequestingToSpeakChanged(bool show) =>
            approveDenySection.SetActive(show);

        public void SetContextMenuButtonVisibility(bool show)
        {
            contextMenuButton.gameObject.SetActive(show);
        }

        public void SetParent(Transform parent)
        {
            transform.SetParent(parent);
            transform.localScale = Vector3.one;
        }

        public void SetupParticipantProfile(
            string? participantName,
            Color nameColor,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            ReactiveProperty<string?> profilePictureUrl,
            string walletId, CancellationToken ct)
        {
            nameElement.text = participantName;
            nameElement.color = nameColor;
            profilePictureView.SetupAsync(profileRepositoryWrapper, nameColor, profilePictureUrl, walletId, ct).Forget();
        }

        private void OnApproveButtonClicked()
        {
            ApproveSpeaker?.Invoke();
            approveDenySection.SetActive(false);
            promotingSpinner.SetActive(true);
        }

        private void OnDenyButtonClicked()
        {
            DenySpeaker?.Invoke();
        }

        private void OnOpenContextMenuClicked()
        {
            OpenContextMenu?.Invoke(contextMenuButton.transform.position);
        }

        private void OnOpenPassportClicked()
        {
            OpenPassport?.Invoke();
        }

        private void SetAsIsSpeaking()
        {
            SetSpeakingIconIdleScale();
            stateSection.SetActive(true);
            isMutedIcon.SetActive(false);
            isSpeakingIcon.gameObject.SetActive(true);
            stateText.SetText(IS_SPEAKING_TEXT);
            ConfigureSequence();

            isSpeakingCurrentSequence.Play();
            return;

            void ConfigureSequence()
            {
                isSpeakingCurrentSequence = DOTween.Sequence();
                isSpeakingCurrentSequence.Append(isSpeakingIconRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconOuterRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Append(isSpeakingIconOuterRect.DOScaleY(0.2f, ANIMATION_DURATION));
                isSpeakingCurrentSequence.Join(isSpeakingIconRect.DOScaleY(1, ANIMATION_DURATION));
                isSpeakingCurrentSequence.SetLoops(-1);
            }
        }

        private void SetAsListener()
        {
            SetSpeakingIconIdleScale();
            approveDenySection.SetActive(false);
            stateSection.SetActive(false);
            promotingSpinner.SetActive(false);
        }

        private void SetAsMuted()
        {
            SetSpeakingIconIdleScale();
            approveDenySection.SetActive(false);
            stateSection.SetActive(true);
            isMutedIcon.SetActive(true);
            isSpeakingIcon.gameObject.SetActive(false);
            stateText.SetText(IS_MUTED_TEXT);
        }

        private void SetAsSpeaker()
        {
            approveDenySection.SetActive(false);
            stateSection.SetActive(false);
            promotingSpinner.SetActive(false);
            SetSpeakingIconIdleScale();
        }

        private void SetSpeakingIconIdleScale()
        {
            isSpeakingCurrentSequence?.Kill();
            isSpeakingCurrentSequence = null;
            isSpeakingIconRect.localScale = IDLE_SCALE;
            isSpeakingIconOuterRect.localScale = IDLE_SCALE;
        }
    }
}
