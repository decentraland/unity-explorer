using DCL.Profiles;
using System;
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

        public Profile UserProfile { get; protected set; }

        private void Start()
        {
            Background.color = NormalColor;
        }

        public virtual void Configure(Profile profile)
        {
            UnHover();
            UserProfile = profile;
        }

        protected virtual void ToggleButtonView(bool isActive)
        {
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].gameObject.SetActive(isActive);
        }

        protected void UnHover()
        {
            ToggleButtonView(false);
            Background.color = NormalColor;
        }

        protected void Hover()
        {
            ToggleButtonView(true);
            Background.color = HoveredColor;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            Hover();

        public void OnPointerExit(PointerEventData eventData) =>
            UnHover();
    }
}
