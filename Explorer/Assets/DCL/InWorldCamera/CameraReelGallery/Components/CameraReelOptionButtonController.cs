using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class CameraReelOptionButtonController : IDisposable
    {
        private const int PUBLIC_CONTROL_INDEX = 0;
        private const int SHARE_CONTROL_INDEX = 2;
        private const int COPY_CONTROL_INDEX = 3;
        private const int DOWNLOAD_CONTROL_INDEX = 4;
        private const int DELETE_CONTROL_INDEX = 5;

        private readonly CameraReelOptionButtonView view;
        private readonly RectTransform buttonRectTransform;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenuConfig contextMenuConfig;
        private readonly Dictionary<ContextMenuControlSettings, Delegate> controlsActions;
        private readonly Dictionary<ContextMenuControlSettings, object> initialValues = new ();

        public event Action? Hide;

        public event Action<CameraReelResponseCompact, bool>? SetPublicRequested;
        public event Action<CameraReelResponseCompact>? ShareToXRequested;
        public event Action<CameraReelResponseCompact>? CopyPictureLinkRequested;
        public event Action<CameraReelResponseCompact>? DownloadRequested;
        public event Action<CameraReelResponseCompact>? DeletePictureRequested;

        private bool isContextMenuOpen;
        private CameraReelResponseCompact currentReelData;
        private UniTaskCompletionSource closeContextMenuTask;

        public CameraReelOptionButtonController(CameraReelOptionButtonView view,
            IMVCManager mvcManager,
            GenericContextMenuConfig contextMenuConfig)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.contextMenuConfig = contextMenuConfig;
            this.buttonRectTransform = view.GetComponent<RectTransform>();

            this.view.optionButton.onClick.AddListener(OnOptionClicked);

            controlsActions = new ()
            {
                { contextMenuConfig.ContextMenuSettings[PUBLIC_CONTROL_INDEX], new Action<bool>(toggleValue => SetPublicRequested?.Invoke(currentReelData, toggleValue)) },
                { contextMenuConfig.ContextMenuSettings[SHARE_CONTROL_INDEX], new Action(() => ShareToXRequested?.Invoke(currentReelData)) },
                { contextMenuConfig.ContextMenuSettings[COPY_CONTROL_INDEX], new Action(() => CopyPictureLinkRequested?.Invoke(currentReelData)) },
                { contextMenuConfig.ContextMenuSettings[DOWNLOAD_CONTROL_INDEX], new Action(() => DownloadRequested?.Invoke(currentReelData)) },
                { contextMenuConfig.ContextMenuSettings[DELETE_CONTROL_INDEX], new Action(() => DeletePictureRequested?.Invoke(currentReelData)) },
            };
        }

        public bool IsContextMenuOpen() => isContextMenuOpen;

        public void Show(CameraReelResponseCompact cameraReelResponse, Transform parent, Vector3 offsetPosition = default)
        {
            view.transform.SetParent(parent);
            view.transform.localPosition = offsetPosition;
            view.gameObject.SetActive(true);
            currentReelData = cameraReelResponse;
            initialValues[contextMenuConfig.ContextMenuSettings[PUBLIC_CONTROL_INDEX]] = cameraReelResponse.isPublic;
            closeContextMenuTask = new UniTaskCompletionSource();
        }

        public void HideControl()
        {
            view.transform.localScale = Vector3.one;
            closeContextMenuTask?.TrySetResult();
            Hide?.Invoke();
            view.gameObject.SetActive(false);
        }

        private void OnOptionClicked()
        {
            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(
                new GenericContextMenuParameter(contextMenuConfig, controlsActions, buttonRectTransform.position,
                    actionOnShow: () => isContextMenuOpen = true,
                    actionOnHide: () =>
                    {
                        isContextMenuOpen = false;
                        HideControl();
                    },
                    closeTask: closeContextMenuTask?.Task,
                    initialValues: initialValues)));
        }

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            Hide = null;
        }
    }
}
