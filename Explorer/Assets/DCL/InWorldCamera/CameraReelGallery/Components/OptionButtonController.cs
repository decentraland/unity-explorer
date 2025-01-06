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
    public class OptionButtonController : IDisposable
    {
        private readonly OptionButtonView view;
        private readonly RectTransform buttonRectTransform;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenuConfig contextMenuConfig;
        private readonly Dictionary<int, Delegate> controlsActions;
        private readonly Dictionary<int, object> initialValues = new ();

        public event Action? Hide;

        public event Action<CameraReelResponseCompact, bool>? SetPublicRequested;
        public event Action<CameraReelResponseCompact>? ShareToXRequested;
        public event Action<CameraReelResponseCompact>? CopyPictureLinkRequested;
        public event Action<CameraReelResponseCompact>? DownloadRequested;
        public event Action<CameraReelResponseCompact>? DeletePictureRequested;

        private bool isContextMenuOpen;
        private CameraReelResponseCompact currentReelData;
        private UniTaskCompletionSource closeContextMenuTask;

        public OptionButtonController(OptionButtonView view,
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
                { (int)CameraReelGalleryController.ContextMenuControls.PUBLIC_CONTROL_INDEX, new Action<bool>(toggleValue => SetPublicRequested?.Invoke(currentReelData, toggleValue)) },
                { (int)CameraReelGalleryController.ContextMenuControls.SHARE_CONTROL_INDEX, new Action(() => ShareToXRequested?.Invoke(currentReelData)) },
                { (int)CameraReelGalleryController.ContextMenuControls.COPY_CONTROL_INDEX, new Action(() => CopyPictureLinkRequested?.Invoke(currentReelData)) },
                { (int)CameraReelGalleryController.ContextMenuControls.DOWNLOAD_CONTROL_INDEX, new Action(() => DownloadRequested?.Invoke(currentReelData)) },
                { (int)CameraReelGalleryController.ContextMenuControls.DELETE_CONTROL_INDEX, new Action(() => DeletePictureRequested?.Invoke(currentReelData)) },
            };
        }

        public bool IsContextMenuOpen() => isContextMenuOpen;

        public void Show(CameraReelResponseCompact cameraReelResponse, Transform parent, Vector3 offsetPosition = default)
        {
            view.transform.SetParent(parent);
            view.transform.localPosition = offsetPosition;
            view.gameObject.SetActive(true);
            currentReelData = cameraReelResponse;
            initialValues[(int)CameraReelGalleryController.ContextMenuControls.PUBLIC_CONTROL_INDEX] = cameraReelResponse.isPublic;
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
                    actionOnHide: () => isContextMenuOpen = false,
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
