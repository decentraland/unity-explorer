using DCL.UI.ProfileElements;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class PlayerEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<VoiceChatMember, Vector2, PlayerEntryView>? ContextMenuButtonClicked;

        [SerializeField] private RectTransform hoverElement;

        [SerializeField] private Button contextMenuButton;

        [SerializeField] public SimpleProfileView profileView;

        private VoiceChatMember userProfile;

        private void Start()
        {
            userProfile = new VoiceChatMember()
            {
                WalletId = "0x012414blablablabla138g",
                IsModerator = true,
                IsSpeaker = false
            };
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(() => ContextMenuButtonClicked?.Invoke(userProfile!, contextMenuButton.transform.position, this));
        }

        public void SubscribeToInteractions(Action<VoiceChatMember, Vector2, PlayerEntryView> contextMenu)
        {
            RemoveAllListeners();

            ContextMenuButtonClicked += contextMenu;
        }

        private void RemoveAllListeners()
        {
            ContextMenuButtonClicked = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverElement.gameObject.SetActive(false);
        }
    }
}
