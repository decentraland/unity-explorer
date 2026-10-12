using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Credits
{
    public class CreditsPanelView : MonoBehaviour, ICreditsPanelView
    {
        [field: SerializeField]
        public TMP_Text CurrentCredits { get; private set; } = null!;

        [field: SerializeField]
        public Button GetCreditsButton { get; private set; } = null!;

        public bool IsShown
        {
            set => gameObject.SetActive(value);
        }

        public bool IsTopUpEnabled
        {
            set => GetCreditsButton.gameObject.SetActive(value);
        }

        public string Credits
        {
            set => CurrentCredits.text = value;
        }

        public Action? GetCreditsClicked { get; set; }

        private void Awake() =>
            GetCreditsButton.onClick.AddListener(OnGetCreditsClicked);

        private void OnDestroy() =>
            GetCreditsButton.onClick.RemoveListener(OnGetCreditsClicked);

        private void OnGetCreditsClicked() =>
            GetCreditsClicked?.Invoke();
    }
}
