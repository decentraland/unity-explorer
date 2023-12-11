using UnityEngine;

namespace DCL.Minimap
{
    public class SideMenuController
    {
        private readonly SideMenuView view;

        public SideMenuController(SideMenuView view)
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
    }
}
