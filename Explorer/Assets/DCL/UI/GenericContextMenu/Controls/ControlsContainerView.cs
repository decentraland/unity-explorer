using Cysharp.Threading.Tasks;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.Controls
{
    public class ControlsContainerView : MonoBehaviour
    {
        private const float NON_INTERACTABLE_TOOLTIP_ANCHORED_Y_POSITION = 7f;
        private const int NON_INTERACTABLE_TOOLTIP_AUTO_HIDE_DELAY_MS = 5000;

        [SerializeField] internal RectTransform controlsContainer;
        [SerializeField] internal VerticalLayoutGroup controlsLayoutGroup;
        [SerializeField] internal GameObject containerRim;
        [SerializeField] private GameObject loadingAnimation;
        [SerializeField] private RectTransform nonInteractableTooltipContainer;
        [SerializeField] private TMP_Text nonInteractableTooltipText;

        private CancellationTokenSource cts;

        public void SetLoadingAnimationVisibility(bool isVisible) =>
            loadingAnimation.gameObject.SetActive(isVisible);

        public void ShowNonInteractableTooltip(Transform parent, string text)
        {
            cts = cts.SafeRestart();
            ShowNonInteractableTooltipAsync(cts.Token).Forget();
            return;

            async UniTaskVoid ShowNonInteractableTooltipAsync(CancellationToken ct)
            {
                nonInteractableTooltipContainer.gameObject.SetActive(true);
                nonInteractableTooltipContainer.SetParent(parent);
                nonInteractableTooltipContainer.anchoredPosition = new Vector2(nonInteractableTooltipContainer.anchoredPosition.x, NON_INTERACTABLE_TOOLTIP_ANCHORED_Y_POSITION);
                nonInteractableTooltipText.text = text;

                await UniTask.Delay(NON_INTERACTABLE_TOOLTIP_AUTO_HIDE_DELAY_MS, cancellationToken: ct);
                HideNonInteractableTooltip();
            }
        }

        public void HideNonInteractableTooltip()
        {
            cts.SafeCancelAndDispose();
            nonInteractableTooltipContainer.gameObject.SetActive(false);
        }
    }
}
