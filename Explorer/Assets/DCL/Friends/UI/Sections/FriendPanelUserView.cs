using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Friends.UI.Sections
{
    public class FriendPanelUserView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        protected Button[] buttons { get; set; }
        [field: SerializeField] public Image Background { get; private set; }
        [field: SerializeField] public Color NormalColor { get; private set; }
        [field: SerializeField] public Color HoveredColor { get; private set; }

        public string UserWalletAddress { get; protected set; }

        private void Start()
        {
            Background.color = NormalColor;
        }

        protected virtual void ToggleButtonView(bool isActive)
        {
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].gameObject.SetActive(isActive);
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
