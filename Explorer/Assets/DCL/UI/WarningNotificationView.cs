using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class WarningNotificationView : MonoBehaviour
    {
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        [field: SerializeField] public TMP_Text Text { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }

        public bool WasEverClosed { get; private set; }

        private void Awake() =>
            CloseButton.onClick.AddListener(() =>
            {
                WasEverClosed = true;
                Hide();
            });

        public void SetText(string text) =>
            Text.text = text;

        public void Show()
        {
            CanvasGroup.DOFade(1, 0.3f);
            CanvasGroup.interactable = true;
            CanvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            CanvasGroup.DOFade(0, 0.3f);
            CanvasGroup.interactable = false;
            CanvasGroup.blocksRaycasts = false;
        }
    }
}
