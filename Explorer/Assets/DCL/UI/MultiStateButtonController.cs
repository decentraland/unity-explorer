using System;

namespace DCL.UI
{
    public class MultiStateButtonController
    {
        public event Action<bool>? OnButtonClicked;

        private readonly MultiStateButtonView view;
        private readonly bool replacesImage;

        private bool isButtonOn;

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
        }

        public void SetButtonState(bool isOn)
        {
            isButtonOn = isOn;
            SetButtonGraphic(isOn);
        }

        private void ButtonClicked()
        {
            isButtonOn = !isButtonOn;
            SetButtonGraphic(isButtonOn);
            OnButtonClicked?.Invoke(isButtonOn);
        }

        private void SetButtonGraphic(bool isOn)
        {
            view.buttonImageFill.SetActive(isOn);

            if (replacesImage)
                view.buttonImageOutline.SetActive(!isOn);
        }
    }
}
