using DCL.Chat;
using DCL.Profiles;
using System;
using TMPro;
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

        [field: Header("User")]
        [field: SerializeField] public ChatEntryConfigurationSO ChatEntryConfiguration { get; private set; }
        [field: SerializeField] public TMP_Text UserName { get; private set; }
        [field: SerializeField] public TMP_Text UserNameTag { get; private set; }
        [field: SerializeField] public Image FaceFrame { get; private set; }
        [field: SerializeField] public Image FaceRim { get; private set; }

        public Profile UserProfile { get; protected set; }

        private void Start()
        {
            Background.color = NormalColor;
        }

        public virtual void Configure(Profile profile)
        {
            UnHover();
            UserProfile = profile;

            Color userColor = ChatEntryConfiguration.GetNameColor(profile.Name);

            UserName.text = profile.Name;
            UserName.color = userColor;
            UserNameTag.text = $"#{profile.UserId[^4..]}";
            UserNameTag.gameObject.SetActive(!profile.HasClaimedName);
            FaceFrame.color = userColor;
            userColor.r += 0.3f;
            userColor.g += 0.3f;
            userColor.b += 0.3f;
            FaceRim.color = userColor;
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
