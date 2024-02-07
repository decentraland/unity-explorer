using MVC;
using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace DCL.Chat
{
    public class ChatView : ViewBase, IView, IPointerEnterHandler, IPointerExitHandler
    {
        private const float BACKGROUND_FADE_TIME = 0.2f;

        [field: SerializeField]
        public Transform MessagesContainer { get; private set; }

        [field: SerializeField]
        public TMP_InputField InputField { get; private set; }

        [field: SerializeField]
        public CharacterCounterView CharacterCounter { get; private set; }

        [field: SerializeField]
        public CanvasGroup PanelBackgroundCanvasGroup { get; private set; }

        private void Start()
        {
            PanelBackgroundCanvasGroup.alpha = 0;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PanelBackgroundCanvasGroup.DOFade(1, BACKGROUND_FADE_TIME);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PanelBackgroundCanvasGroup.DOFade(0, BACKGROUND_FADE_TIME);
        }
    }
}
