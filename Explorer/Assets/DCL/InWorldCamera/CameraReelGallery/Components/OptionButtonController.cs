using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class OptionButtonController : IDisposable
    {
        private readonly OptionButtonView view;
        private readonly ContextMenuController contextMenuController;
        private readonly RectTransform buttonRectTransform;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenuConfig contextMenuConfig;
        private readonly Dictionary<int, Delegate> controlsActions = new ()
        {
            { 0, new Action<bool>(toggleValue => Debug.Log($"Toggle clicked: {toggleValue}")) },
            { 2, new Action(() => Debug.Log("Share on X clicked")) },
            { 3, new Action(() => Debug.Log("Copy Link clicked")) },
            { 4, new Action(() => Debug.Log("Download clicked")) },
            { 5, new Action(() => Debug.Log("Delete clicked")) },
        };

        public event Action? Hide;

        public OptionButtonController(OptionButtonView view,
            ContextMenuController contextMenuController,
            IMVCManager mvcManager,
            GenericContextMenuConfig contextMenuConfig)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.contextMenuConfig = contextMenuConfig;
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
            // if (!contextMenuController.IsOpen())
            //     contextMenuController.Show(buttonRectTransform.position);
            // else
            //     contextMenuController.Hide();
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenuConfig, controlsActions, buttonRectTransform.position)));
        }

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            contextMenuController.Dispose();
            Hide = null;
        }
    }
}
