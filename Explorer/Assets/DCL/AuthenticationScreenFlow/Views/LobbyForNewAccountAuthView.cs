using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Utility.Extensions;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class LobbyForNewAccountAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public NameInputFieldView ProfileNameInputField { get; private set; } = null!;

        [field: SerializeField]
        public Button BackButton { get; private set; } = null!;

        [field: SerializeField]
        public Button FinalizeNewUserButton { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public Button RandomizeButton { get; private set; } = null!;

        [field: Header("Body Type Selector")]
        [field: SerializeField]
        public Button BodyTypeDropdownButton { get; private set; } = null!;
        [field: SerializeField]
        public GameObject BodyTypeDropdownPanel { get; private set; } = null!;
        [field: SerializeField]
        public Button BodyTypeOptionA { get; private set; } = null!;
        [field: SerializeField]
        public Button BodyTypeOptionB { get; private set; } = null!;
        [field: SerializeField]
        public TMPro.TMP_Text BodyTypeLabel { get; private set; } = null!;
        [field: SerializeField]
        public RectTransform ChevronIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject DropdownManIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject DropdownWomanIcon { get; private set; } = null!;
        [field: SerializeField]
        public GameObject CheckmarkIconA { get; private set; } = null!;
        [field: SerializeField]
        public GameObject CheckmarkIconB { get; private set; } = null!;

        [field: Space]
        [field: SerializeField]
        public Toggle SubscribeToggle { get; private set; } = null!;
        [field: SerializeField]
        public Toggle TermsOfUse { get; private set; } = null!;

        [field: SerializeField]
        public TMP_Text_ClickeableLink TermsOfUseAndPrivacyLink { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;

        private int hideAnimHash = UIAnimationHashes.OUT;

        public void SetBodyTypeDropdownOpen(bool isOpen)
        {
            BodyTypeDropdownPanel.SetActive(isOpen);
            ChevronIcon.localRotation = Quaternion.Euler(0, 0, isOpen ? 180f : 0f);
        }

        public void UpdateBodyTypeUI(bool isMale)
        {
            BodyTypeLabel.text = isMale ? "BODY TYPE A" : "BODY TYPE B";

            DropdownManIcon.SetActive(isMale);
            DropdownWomanIcon.SetActive(!isMale);

            CheckmarkIconA.SetActive(isMale);
            CheckmarkIconB.SetActive(!isMale);

            UpdateBodyTypeLabelAsync(isMale).Forget();
        }

        private async UniTaskVoid UpdateBodyTypeLabelAsync(bool isMale)
        {
            string key = isMale ? "BODY_TYPE_A" : "BODY_TYPE_B";

            try
            {
                var localized = new LocalizedString("Authentication", key);

                AsyncOperationHandle<string> handle = localized.GetLocalizedStringAsync();
                await handle;

                if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded
                                     && !string.IsNullOrEmpty(handle.Result))
                    BodyTypeLabel.text = handle.Result;
            }
            catch
            {
                // keep fallback already set in UpdateBodyTypeUI
            }
        }

        public void Show()
        {
            ShowAsync(CancellationToken.None).Forget();
        }

        public void Hide(int animHash)
        {
            hideAnimHash = animHash;
            HideAsync(CancellationToken.None).Forget();
        }

        public override async UniTask ShowAsync(CancellationToken ct)
        {
            await base.ShowAsync(ct);
            canvasGroup.interactable = true;
        }

        public override async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            canvasGroup.interactable = false;
            await base.HideAsync(ct, isInstant);
        }

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(UIAnimationHashes.IN, UIAnimationHashes.IN, ct: ct);

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(hideAnimHash, hideAnimHash, ct: ct);
    }
}
