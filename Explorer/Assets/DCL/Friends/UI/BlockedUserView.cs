using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Friends.UI
{
    public class BlockedUserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: SerializeField] public Button UnblockButton { get; private set; }
        [field: SerializeField] public Button ContextMenuButton { get; private set; }
        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }

        public string UserWalletAddress { get; set; }

        private void Start()
        {
            Background.color = NormalColor;
        }

        private void ToggleButtonView(bool isActive)
        {
            UnblockButton.gameObject.SetActive(isActive);
            ContextMenuButton.gameObject.SetActive(isActive);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ToggleButtonView(true);
            Background.color = HoveredColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ToggleButtonView(false);
            Background.color = NormalColor;
        }
    }
}
