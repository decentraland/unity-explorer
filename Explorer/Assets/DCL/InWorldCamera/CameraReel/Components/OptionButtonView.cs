using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class OptionButtonView : MonoBehaviour, IDisposable
    {
        [SerializeField] private GameObject contextMenu;
        [SerializeField] private GameObject hoverHelper;
        [SerializeField] private Button optionButton;

        [Header("Controls")]
        [SerializeField] private Toggle setAsPublic;
        [SerializeField] private Button shareOnX;
        [SerializeField] private Button copyLink;
        [SerializeField] private Button download;
        [SerializeField] private Button delete;

        public event Action<CameraReelResponse, bool> SetPublicRequested;
        public event Action<CameraReelResponse> ShareToXRequested;
        public event Action<CameraReelResponse> CopyPictureLinkRequested;
        public event Action<CameraReelResponse> DownloadRequested;
        public event Action<CameraReelResponse> DeletePictureRequested;

        public CameraReelResponse ImageData { get; private set; }

        private void Awake()
        {
            ResetState();

            optionButton.onClick.AddListener(OnOptionClicked);

            shareOnX.onClick.AddListener(() =>
            {
                ShareToXRequested?.Invoke(ImageData);
                ResetState();
            });

            copyLink.onClick.AddListener(() =>
            {
                CopyPictureLinkRequested?.Invoke(ImageData);
                ResetState();
            });

            download.onClick.AddListener(() =>
            {
                DownloadRequested?.Invoke(ImageData);
                ResetState();
            });

            delete.onClick.AddListener(() =>
            {
                DeletePictureRequested?.Invoke(ImageData);
                ResetState();
            });
        }

        private void SetAsPublicInvoke(bool toggle) =>
            SetPublicRequested?.Invoke(ImageData, toggle);

        private void OnOptionClicked()
        {
            bool active = !contextMenu.activeSelf;
            contextMenu.SetActive(active);
            hoverHelper.SetActive(active);

            if (!active) return;

            //Align the "public" toggle status according to the imageData without triggering an "invoke"
            setAsPublic.onValueChanged.RemoveListener(SetAsPublicInvoke);
            setAsPublic.isOn = ImageData.isPublic;
            setAsPublic.onValueChanged.AddListener(SetAsPublicInvoke);
        }

        public void SetImageData(CameraReelResponse cameraReelResponse) =>
            ImageData = cameraReelResponse;

        public void ResetState()
        {
            contextMenu.SetActive(false);
            hoverHelper.SetActive(false);
            this.transform.localScale = Vector3.one;
        }

        public void Dispose()
        {
            optionButton.onClick.RemoveAllListeners();
            setAsPublic.onValueChanged.RemoveAllListeners();
            shareOnX.onClick.RemoveAllListeners();
            copyLink.onClick.RemoveAllListeners();
            download.onClick.RemoveAllListeners();
            delete.onClick.RemoveAllListeners();
            SetPublicRequested = null;
            ShareToXRequested = null;
            CopyPictureLinkRequested = null;
            DownloadRequested = null;
            DeletePictureRequested = null;
        }
    }
}
