using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.ExplorePanel.Lobby
{
    /// <summary>Shows the same customization affordance for pointer and keyboard users.</summary>
    public sealed class LobbyAvatarAffordance : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private GameObject hint;
        [SerializeField] private Outline highlight;

        public void OnPointerEnter(PointerEventData _) => SetHighlighted(true);
        public void OnPointerExit(PointerEventData _) => SetHighlighted(false);
        public void OnSelect(BaseEventData _) => SetHighlighted(true);
        public void OnDeselect(BaseEventData _) => SetHighlighted(false);

        private void OnDisable()
        {
            SetHighlighted(false);
        }

        private void SetHighlighted(bool highlighted)
        {
            hint.SetActive(highlighted);
            highlight.enabled = highlighted;
        }
    }
}
