using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class WarningNotificationView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Text { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }

        public bool WasEverClosed { get; private set; }

        private void Awake() =>
            CloseButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                WasEverClosed = true;
            });

        public void SetText(string text) => Text.text = text;
    }
}
