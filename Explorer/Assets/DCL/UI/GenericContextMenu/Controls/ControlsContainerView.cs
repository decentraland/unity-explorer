using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class ControlsContainerView : MonoBehaviour
    {
        [SerializeField] internal RectTransform controlsContainer;
        [SerializeField] internal VerticalLayoutGroup controlsLayoutGroup;
        [SerializeField] internal GameObject containerRim;
        [SerializeField] private GameObject loadingAnimation;
        [SerializeField] private RectTransform nonInteractableTooltipContainer;
        [SerializeField] private TMP_Text nonInteractableTooltipText;

        public void SetLoadingAnimationVisibility(bool isVisible) =>
            loadingAnimation.gameObject.SetActive(isVisible);

        public void ShowNonInteractableTooltip(Transform parent, string text)
        {
            nonInteractableTooltipContainer.gameObject.SetActive(true);
            nonInteractableTooltipContainer.SetParent(parent);
            nonInteractableTooltipContainer.anchoredPosition = new Vector2(nonInteractableTooltipContainer.anchoredPosition.x, 7f);
            nonInteractableTooltipText.text = text;
        }

        public void HideNonInteractableTooltip() =>
            nonInteractableTooltipContainer.gameObject.SetActive(false);
    }
}
