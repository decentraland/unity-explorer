using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class ControlsContainerView : MonoBehaviour
    {
        [SerializeField] internal RectTransform controlsContainer;
        [SerializeField] internal VerticalLayoutGroup controlsLayoutGroup;
        [SerializeField] internal GameObject containerRim;
        [SerializeField] private GameObject loadingAnimation;

        public void SetLoadingAnimationVisibility(bool isVisible) =>
            loadingAnimation.gameObject.SetActive(isVisible);
    }
}
