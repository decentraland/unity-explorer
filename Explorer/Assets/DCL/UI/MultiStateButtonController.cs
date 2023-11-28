using System;

namespace DCL.UI
{
    public class MultiStateButtonController
    {
        public event Action<bool> OnButtonClicked;

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

        private void ButtonClicked()
        {
            isButtonOn = !isButtonOn;
            SetButtonGraphic(isButtonOn);
            OnButtonClicked?.Invoke(isButtonOn);
        }

        public void SetButtonState(bool isOn)
        {
            isButtonOn = isOn;
            SetButtonGraphic(isOn);
        }

        private void SetButtonGraphic(bool isOn)
        {
            view.buttonImageFill.SetActive(isOn);

            if (replacesImage)
                view.buttonImageOutline.SetActive(!isOn);
        }
    }
}
