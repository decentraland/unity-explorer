using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class CameraReelOptionButtonController : IDisposable
    {
        private readonly CameraReelOptionButtonView view;
        private readonly RectTransform buttonRectTransform;
        private readonly IMVCManager mvcManager;
        private readonly GenericContextMenu contextMenu;
        private readonly ToggleContextMenuControlSettings publicToggleSettings;

        public event Action? Hide;

        public event Action<CameraReelResponseCompact, bool>? SetPublicRequested;
        public event Action<CameraReelResponseCompact>? ShareToXRequested;
        public event Action<CameraReelResponseCompact>? CopyPictureLinkRequested;
        public event Action<CameraReelResponseCompact>? DownloadRequested;
        public event Action<CameraReelResponseCompact>? DeletePictureRequested;

        private bool isContextMenuOpen;
        private CameraReelResponseCompact currentReelData;
        private UniTaskCompletionSource? closeContextMenuTask;

        public CameraReelOptionButtonController(CameraReelOptionButtonView view,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.buttonRectTransform = view.GetComponent<RectTransform>();

            this.view.optionButton.onClick.AddListener(OnOptionClicked);

            contextMenu = new GenericContextMenu()
               .AddControl(publicToggleSettings = new ToggleContextMenuControlSettings(view.publicToggleText, toggleValue => SetPublicRequested?.Invoke(currentReelData, toggleValue)))
               .AddControl(new SeparatorContextMenuControlSettings())
               .AddControl(new ButtonContextMenuControlSettings(view.shareButtonText, view.shareButtonSprite, () => ShareToXRequested?.Invoke(currentReelData)))
               .AddControl(new ButtonContextMenuControlSettings(view.copyButtonText, view.copyButtonSprite, () => CopyPictureLinkRequested?.Invoke(currentReelData)))
               .AddControl(new ButtonContextMenuControlSettings(view.downloadButtonText, view.downloadButtonSprite, () => DownloadRequested?.Invoke(currentReelData)))
               .AddControl(new ButtonContextMenuControlSettings(view.deleteButtonText, view.deleteButtonSprite, () => DeletePictureRequested?.Invoke(currentReelData)));
        }

        public bool IsContextMenuOpen() => isContextMenuOpen;

        public void Show(CameraReelResponseCompact cameraReelResponse, Transform parent, Vector3 offsetPosition = default)
        {
            view.transform.SetParent(parent);
            view.transform.localPosition = offsetPosition;
            view.gameObject.SetActive(true);
            currentReelData = cameraReelResponse;
            publicToggleSettings.SetInitialValue(cameraReelResponse.isPublic);
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
                new GenericContextMenuParameter(contextMenu, buttonRectTransform.position,
                    actionOnShow: () => isContextMenuOpen = true,
                    actionOnHide: () =>
                    {
                        isContextMenuOpen = false;
                        if (view.gameObject.activeSelf)
                            HideControl();
                    },
                    closeTask: closeContextMenuTask?.Task)));
        }

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            Hide = null;
        }
    }
}
