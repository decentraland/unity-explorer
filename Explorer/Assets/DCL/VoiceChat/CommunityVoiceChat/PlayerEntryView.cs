using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.VoiceChat.CommunityVoiceChat
{
    public class PlayerEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private RectTransform hoverElement;

        [SerializeField] private Button contextMenuButton;

        private void Start()
        {
            hoverElement.gameObject.SetActive(false);
            contextMenuButton.onClick.AddListener(OnContextMenuClicked);
        }

        private void OnContextMenuClicked()
        {

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
