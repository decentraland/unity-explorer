using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SelectorButton
{
    public class SelectorButtonOptionItemView : MonoBehaviour
    {
        public event Action<SelectorButtonOptionItemView>? Clicked;
        public event Action? VisibilityChanged;

        [SerializeField] private Button? optionButton;
        [SerializeField] private TMP_Text? optionText;

        public string OptionTitle => optionText != null ? optionText.text : string.Empty;
        public bool IsHidden => !this.gameObject.activeSelf;

        private void OnEnable() =>
            optionButton?.onClick.AddListener(OnClick);

        private void OnDisable() =>
            optionButton?.onClick.RemoveListener(OnClick);

        public void Setup(string optionTextValue)
        {
            if (optionText == null)
                return;

            optionText.text = optionTextValue;
        }

        public void SetHidden(bool isHidden)
        {
            this.gameObject.SetActive(!isHidden);
            VisibilityChanged?.Invoke();
        }

        private void OnClick() =>
            Clicked?.Invoke(this);
    }
}
