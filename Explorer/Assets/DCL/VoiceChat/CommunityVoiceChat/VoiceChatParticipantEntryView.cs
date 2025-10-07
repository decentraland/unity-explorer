using DCL.UI.ProfileElements;
using DG.Tweening;
using System;
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

        public event Action<Vector2>? ContextMenuButtonClicked;
        public event Action? ApproveSpeaker;
        public event Action? DenySpeaker;
        public event Action? OpenPassport;

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
        [SerializeField] private GameObject statusSection  = null!;

        private Sequence? isSpeakingCurrentSequence;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(OnOpenContextMenuClicked);
            openPassportButton.onClick.AddListener(OnOpenPassportClicked);
            approveButton.onClick.AddListener(OnApproveButtonClicked);
            denyButton.onClick.AddListener(OnDenyButtonClicked);
        }

        private void OnOpenContextMenuClicked()
        {
            ContextMenuButtonClicked?.Invoke(contextMenuButton.transform.position);
        }

        public void CleanupEntry()
        {
            isSpeakingCurrentSequence?.Kill();
            isSpeakingCurrentSequence = null;
            SetSpeakingIconIdleScale();
            approveDenySection.SetActive(false);
            IsSpeakingIcon.gameObject.SetActive(false);
            isMutedIcon.SetActive(false);
            promotingSpinner.SetActive(false);
            statusText.SetText(IS_SPEAKING_TEXT);
        }

        private void OnOpenPassportClicked()
        {
            OpenPassport?.Invoke();
        }

        private void OnDenyButtonClicked()
        {
            DenySpeaker?.Invoke();
        }

        private void OnApproveButtonClicked()
        {
            ApproveSpeaker?.Invoke();
            approveDenySection.SetActive(false);
            promotingSpinner.SetActive(true);
        }

        public void OnIsMutedChanged(bool isMuted, bool isSpeaker)
        {
            if (!isSpeaker)
            {
                statusText.text = string.Empty;
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
                statusSection.gameObject.SetActive(false);
                promotingSpinner.gameObject.SetActive(false);
                return;
            }

            if (isMuted)
            {
                statusText.text = IS_MUTED_TEXT;
                isSpeakingCurrentSequence?.Kill();
                isSpeakingCurrentSequence = null;
                IsSpeakingIcon.gameObject.SetActive(false);
                isMutedIcon.SetActive(true);
            }
            else if (isSpeaker)
            {
                statusText.text = IS_SPEAKING_TEXT;
                IsSpeakingIcon.gameObject.SetActive(true);
                isMutedIcon.SetActive(false);
                SetSpeakingIconIdleScale();
            }
        }

        public void ShowApproveDenySection(bool show) =>
            approveDenySection.SetActive(show);

        public void OnChangeIsSpeaking(bool isSpeaking)
        {
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

        public void ConfigureTransform(Transform parent, Vector3 scale)
        {
            transform.SetParent(parent);
            transform.localScale = scale;
        }

        public void SetupNameElement(string? participantName, Color nameColor)
        {
            NameElement.text = participantName;
            NameElement.color = nameColor;
            SetSpeakingIconIdleScale();
        }

        public void SetContextMenuButtonVisibility(bool show)
        {
            contextMenuButton.gameObject.SetActive(show);
        }
    }
}
