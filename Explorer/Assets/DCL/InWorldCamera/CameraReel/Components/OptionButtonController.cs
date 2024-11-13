using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class OptionButtonController : IDisposable
    {
        private readonly OptionButtonView view;

        public event Action<CameraReelResponse, bool> SetPublicRequested;
        public event Action<CameraReelResponse> ShareToXRequested;
        public event Action<CameraReelResponse> CopyPictureLinkRequested;
        public event Action<CameraReelResponse> DownloadRequested;
        public event Action<CameraReelResponse> DeletePictureRequested;

        private CameraReelResponse imageData;

        public OptionButtonController(OptionButtonView view)
        {
            this.view = view;

            this.view.optionButton.onClick.AddListener(OnOptionClicked);

            this.view.shareOnX.onClick.AddListener(() =>
            {
                ShareToXRequested?.Invoke(imageData);
                view.ResetState();
            });

            this.view.copyLink.onClick.AddListener(() =>
            {
                CopyPictureLinkRequested?.Invoke(imageData);
                view.ResetState();
            });

            this.view.download.onClick.AddListener(() =>
            {
                DownloadRequested?.Invoke(imageData);
                view.ResetState();
            });

            this.view.delete.onClick.AddListener(() =>
            {
                DeletePictureRequested?.Invoke(imageData);
                view.ResetState();
            });
        }

        public void ResetViewState() => view.ResetState();

        public GameObject GetViewGameObject() => view.gameObject;

        private void SetAsPublicInvoke(bool toggle) =>
            SetPublicRequested?.Invoke(imageData, toggle);

        private void OnOptionClicked()
        {
            bool active = !view.contextMenu.activeSelf;
            view.contextMenu.SetActive(active);
            view.hoverHelper.SetActive(active);

            if (!active) return;

            //Align the "public" toggle status according to the imageData without triggering an "invoke"
            view.setAsPublic.onValueChanged.RemoveListener(SetAsPublicInvoke);
            view.setAsPublic.isOn = imageData.isPublic;
            view.setAsPublic.onValueChanged.AddListener(SetAsPublicInvoke);
        }

        public void SetImageData(CameraReelResponse cameraReelResponse) =>
            imageData = cameraReelResponse;

        public void Dispose()
        {
            view.optionButton.onClick.RemoveAllListeners();
            view.setAsPublic.onValueChanged.RemoveAllListeners();
            view.shareOnX.onClick.RemoveAllListeners();
            view.copyLink.onClick.RemoveAllListeners();
            view.download.onClick.RemoveAllListeners();
            view.delete.onClick.RemoveAllListeners();
            SetPublicRequested = null;
            ShareToXRequested = null;
            CopyPictureLinkRequested = null;
            DownloadRequested = null;
            DeletePictureRequested = null;
        }
    }
}
