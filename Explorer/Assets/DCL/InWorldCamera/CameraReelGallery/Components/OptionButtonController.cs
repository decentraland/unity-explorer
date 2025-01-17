using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class OptionButtonController : IDisposable
    {
        private readonly OptionButtonView view;
        private readonly ContextMenuController contextMenuController;
        private readonly RectTransform buttonRectTransform;

        public event Action? Hide;

        public OptionButtonController(OptionButtonView view,
            ContextMenuController contextMenuController)
        {
            this.view = view;
            this.buttonRectTransform = view.GetComponent<RectTransform>();

            this.view.optionButton.onClick.AddListener(OnOptionClicked);
            this.contextMenuController = contextMenuController;

            this.contextMenuController.AnyControlClicked += HideControl;
        }

        public bool IsContextMenuOpen() => contextMenuController.IsOpen();

        public void Show(CameraReelResponseCompact cameraReelResponse, Transform parent, Vector3 offsetPosition = default)
        {
            view.transform.SetParent(parent);
            view.transform.localPosition = offsetPosition;
            view.gameObject.SetActive(true);
            contextMenuController.SetImageData(cameraReelResponse);
        }

        public void HideControl()
        {
            view.transform.localScale = Vector3.one;
            contextMenuController.Hide();
            Hide?.Invoke();
            view.gameObject.SetActive(false);
        }

        private void OnOptionClicked()
        {
            if (!contextMenuController.IsOpen())
                contextMenuController.Show(buttonRectTransform.position);
            else
                contextMenuController.Hide();
        }

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            contextMenuController.Dispose();
            Hide = null;
        }
    }
}
