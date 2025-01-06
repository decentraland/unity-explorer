using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class OptionButtonController : IDisposable
    {
        private const int PUBLIC_CONTROL_INDEX = 0;
        private const int SHARE_CONTROL_INDEX = 2;
        private const int COPY_CONTROL_INDEX = 3;
        private const int DOWNLOAD_CONTROL_INDEX = 4;
        private const int DELETE_CONTROL_INDEX = 5;

        private readonly OptionButtonView view;
        private readonly ContextMenuController contextMenuController;
        private readonly RectTransform buttonRectTransform;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenuConfig contextMenuConfig;
        private readonly Dictionary<int, Delegate> controlsActions = new ()
        {
            { PUBLIC_CONTROL_INDEX, new Action<bool>(toggleValue => Debug.Log($"Toggle clicked: {toggleValue}")) },
            { SHARE_CONTROL_INDEX, new Action(() => Debug.Log("Share on X clicked")) },
            { COPY_CONTROL_INDEX, new Action(() => Debug.Log("Copy Link clicked")) },
            { DOWNLOAD_CONTROL_INDEX, new Action(() => Debug.Log("Download clicked")) },
            { DELETE_CONTROL_INDEX, new Action(() => Debug.Log("Delete clicked")) },
        };
        private readonly Dictionary<int, object> initialValues = new ();

        public event Action? Hide;

        private bool isContextMenuOpen;

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

        public bool IsContextMenuOpen() => isContextMenuOpen;

        public void Show(CameraReelResponseCompact cameraReelResponse, Transform parent, Vector3 offsetPosition = default)
        {
            view.transform.SetParent(parent);
            view.transform.localPosition = offsetPosition;
            view.gameObject.SetActive(true);
            contextMenuController.SetImageData(cameraReelResponse);
            initialValues[PUBLIC_CONTROL_INDEX] = cameraReelResponse.isPublic;
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
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenuConfig, controlsActions, buttonRectTransform.position,
                    actionOnShow: () => isContextMenuOpen = true,
                    actionOnHide: () => isContextMenuOpen = false,
                    closeTask: view.optionButton.OnClickAsync(),
                    initialValues: initialValues)));
        }

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            contextMenuController.Dispose();
            Hide = null;
        }
    }
}
