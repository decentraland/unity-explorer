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
        [Header("Controls")]
        [SerializeField] private Toggle setAsPublic;
        [SerializeField] private Button shareOnX;
        [SerializeField] private Button copyLink;
        [SerializeField] private Button download;
        [SerializeField] private Button delete;

        public event Action<CameraReelResponse, bool> OnSetPublicRequested;
        public event Action<CameraReelResponse> OnShareToXRequested;
        public event Action<CameraReelResponse> OnCopyPictureLinkRequested;
        public event Action<CameraReelResponse> OnDownloadRequested;
        public event Action<CameraReelResponse> OnDeletePictureRequested;

        private CameraReelResponse imageData;

        private void Awake()
        {
            ResetState();

            setAsPublic.onValueChanged.AddListener(toggle =>
            {
                OnSetPublicRequested?.Invoke(imageData, toggle);
            });

            shareOnX.onClick.AddListener(() =>
            {
                OnShareToXRequested?.Invoke(imageData);
                ResetState();
            });

            copyLink.onClick.AddListener(() =>
            {
                OnCopyPictureLinkRequested?.Invoke(imageData);
                ResetState();
            });

            download.onClick.AddListener(() =>
            {
                OnDownloadRequested?.Invoke(imageData);
                ResetState();
            });

            delete.onClick.AddListener(() =>
            {
                OnDeletePictureRequested?.Invoke(imageData);
                ResetState();
            });
        }

        public void OnOptionClicked()
        {
            bool active = !contextMenu.activeSelf;
            contextMenu.SetActive(active);
            hoverHelper.SetActive(active);
        }

        public void SetImageData(CameraReelResponse cameraReelResponse) =>
            imageData = cameraReelResponse;

        public void ResetState()
        {
            contextMenu.SetActive(false);
            hoverHelper.SetActive(false);
            this.transform.localScale = Vector3.one;
        }

        public void Dispose()
        {
            setAsPublic.onValueChanged.RemoveAllListeners();
            shareOnX.onClick.RemoveAllListeners();
            copyLink.onClick.RemoveAllListeners();
            download.onClick.RemoveAllListeners();
            delete.onClick.RemoveAllListeners();
        }
    }
}
