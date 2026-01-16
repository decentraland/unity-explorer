using DCL.UI;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class LoginScreenSubView : MonoBehaviour
    {
        private Animator loginAnimator;
        private CanvasGroup canvasGroup;

        [SerializeField] private GameObject loadingSpinner;
        [SerializeField] private GameObject mainElementsPanel;

        [field: Header("OTP")]
        [field: SerializeField]
        public EmailInputFieldView EmailInputField { get; private set; } = null!;

        [field: Header("SECONDARY")]
        [field: SerializeField]
        public Button MetamaskLoginButton { get; private set; } = null!;

        [field: SerializeField]
        public Button GoogleLoginButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelLoginButton { get; private set; } = null!;

        [field: Header("OTHER OPTIONS")]
        [field: SerializeField]
        public Button MoreOptionsButton { get; private set; } = null!;

        [field: SerializeField]
        public RectTransform MoreOptionsButtonDirIcon { get; private set; } = null!;

        [SerializeField]
        private GameObject moreOptionsPanel;

        private bool areOptionsExpanded;

        private void Awake()
        {
            loginAnimator = GetComponent<Animator>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            SlideIn();
        }

        private void OnDisable()
        {
            loginAnimator.enabled = false;
        }

        public void ToggleOptionsPanelExpansion()
        {
            areOptionsExpanded = !areOptionsExpanded;
            SetOptionsPanelVisibility(areOptionsExpanded);
        }

        private void SetOptionsPanelVisibility(bool isExpanded)
        {
            MoreOptionsButtonDirIcon.localScale = new Vector3(1, isExpanded ? -1 : 1, 1);
            moreOptionsPanel.SetActive(isExpanded);
        }

        public void SlideIn()
        {
            loginAnimator.enabled = true;
            loginAnimator.ResetAnimator();
            loginAnimator.SetTrigger(UIAnimationHashes.IN);

            canvasGroup.interactable = true;
            loadingSpinner.SetActive(false);
            mainElementsPanel.SetActive(true);

            // MetamaskLoginButton.gameObject.SetActive(true);
            // MetamaskLoginButton.interactable = true;
            // GoogleLoginButton.gameObject.SetActive(true);
            // GoogleLoginButton.interactable = true;
            // MoreOptionsButton.gameObject.SetActive(true);
            // moreOptionsPanel.SetActive(true);

            areOptionsExpanded = false;
            SetOptionsPanelVisibility(areOptionsExpanded);
        }

        // Anim-OUT non-interactable Login Screen
        public void SlideOut()
        {
            loginAnimator.SetTrigger(UIAnimationHashes.OUT);
            canvasGroup.interactable = false;

            mainElementsPanel.SetActive(true);
            loadingSpinner.SetActive(false);
        }

        public void ShowLoading()
        {
            mainElementsPanel.SetActive(false);
            loadingSpinner.SetActive(true);

            // MetamaskLoginButton.gameObject.SetActive(false);
            // MetamaskLoginButton.interactable = false;
            // GoogleLoginButton.gameObject.SetActive(false);
            // GoogleLoginButton.interactable = false;
            // MoreOptionsButton.gameObject.SetActive(false);
            // moreOptionsPanel.SetActive(false);
        }
    }
}
