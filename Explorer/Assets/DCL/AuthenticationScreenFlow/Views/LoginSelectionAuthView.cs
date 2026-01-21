using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class LoginSelectionAuthView : MonoBehaviour
    {
        public static readonly int IS_SHOWN_ANIM_HASH = Animator.StringToHash("IsShown");

        [SerializeField] private Animator loginAnimator;
        [SerializeField] private CanvasGroup canvasGroup;

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
            loginAnimator.SetBool(IS_SHOWN_ANIM_HASH, true);

            canvasGroup.interactable = true;
            mainElementsPanel.SetActive(true);
            loadingSpinner.SetActive(false);

            areOptionsExpanded = false;
            SetOptionsPanelVisibility(isExpanded: false);
        }

        // Anim-OUT non-interactable Login Screen
        public void SlideOut()
        {
            loginAnimator.SetBool(IS_SHOWN_ANIM_HASH, false);

            canvasGroup.interactable = false;
            mainElementsPanel.SetActive(false);
            loadingSpinner.SetActive(false);
        }

        public void ShowLoading()
        {
            mainElementsPanel.SetActive(false);
            loadingSpinner.SetActive(true);
        }
    }
}
