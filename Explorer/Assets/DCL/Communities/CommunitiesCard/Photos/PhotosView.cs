using DCL.InWorldCamera.CameraReelGallery;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Communities.CommunitiesCard.Photos
{
    public class PhotosView : MonoBehaviour
    {
        [field: SerializeField] public CameraReelGalleryView GalleryView { get; private set; }
        [field: SerializeField] private GameObject adminEmptyText;
        [field: SerializeField] private Button openWizardButton;

        public event Action? OpenWizardButtonClicked;

        private void Awake()
        {
            openWizardButton.onClick.AddListener(() => OpenWizardButtonClicked?.Invoke());
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        public void SetAdminEmptyTextActive(bool active) =>
            adminEmptyText.SetActive(active);

    }
}
