using System;

namespace DCL.UI
{
    public class MultiStateButtonController
    {
        public event Action<bool>? OnButtonClicked;

        private readonly MultiStateButtonView view;
        private readonly bool replacesImage;

        public bool IsButtonOn { get; private set; }

        public MultiStateButtonController(MultiStateButtonView view, bool replacesImage)
        {
            this.view = view;
            this.replacesImage = replacesImage;
            view.button.onClick.RemoveAllListeners();
            view.button.onClick.AddListener(ButtonClicked);
        }

        public void ClearClickListeners()
        {
            view.button.onClick.RemoveAllListeners();
            OnButtonClicked = null;
        }

        public void SetButtonState(bool isOn)
        {
            IsButtonOn = isOn;
            SetButtonGraphic(isOn);
        }

        public void SetButtonInteractable(bool isInteractable)
        {
            view.button.interactable = isInteractable;
        }

        private void ButtonClicked()
        {
            IsButtonOn = !IsButtonOn;
            SetButtonGraphic(IsButtonOn);
            OnButtonClicked?.Invoke(IsButtonOn);
        }

        private void SetButtonGraphic(bool isOn)
        {
            view.buttonImageFill.SetActive(isOn);

            if (replacesImage)
                view.buttonImageOutline.SetActive(!isOn);
        }
    }
}
