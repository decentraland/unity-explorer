using System;

namespace DCL.Minimap
{
    public class SideMenuPresenter : IDisposable
    {
        private readonly SideMenuView view;

        public SideMenuPresenter(SideMenuView view)
        {
            this.view = view;

            view.ToggleHome.Toggle.onValueChanged.AddListener(OnToggleHomeValueChanged);
            view.ToggleSceneUI.Toggle.onValueChanged.AddListener(OnToggleSceneUIValueChanged);
            view.InfoButton.onClick.AddListener(OnInfoButtonClicked);
            view.CopyCoordinatesButton.onClick.AddListener(OnCopyCoordinatesButtonClicked);
            view.CopyLinkButton.onClick.AddListener(OnCopyLinkButtonClicked);
            view.TwitterButton.onClick.AddListener(OnTwitterButtonClicked);
        }

        private void OnTwitterButtonClicked()
        {

        }

        private void OnCopyLinkButtonClicked()
        {

        }

        private void OnCopyCoordinatesButtonClicked()
        {

        }

        private void OnInfoButtonClicked()
        {

        }

        private void OnToggleSceneUIValueChanged(bool isToggleOn)
        {
            view.ToggleSceneUI.OnImage.SetActive(isToggleOn);
            view.ToggleSceneUI.OffImage.SetActive(!isToggleOn);
        }

        private void OnToggleHomeValueChanged(bool isToggleOn)
        {
            view.ToggleHome.OnImage.SetActive(isToggleOn);
            view.ToggleHome.OffImage.SetActive(!isToggleOn);
        }

        public void Dispose()
        {
            view.ToggleHome.Toggle.onValueChanged.RemoveListener(OnToggleHomeValueChanged);
            view.ToggleSceneUI.Toggle.onValueChanged.RemoveListener(OnToggleSceneUIValueChanged);
            view.InfoButton.onClick.RemoveListener(OnInfoButtonClicked);
            view.CopyCoordinatesButton.onClick.RemoveListener(OnCopyCoordinatesButtonClicked);
            view.CopyLinkButton.onClick.RemoveListener(OnCopyLinkButtonClicked);
            view.TwitterButton.onClick.RemoveListener(OnTwitterButtonClicked);
        }
    }
}
