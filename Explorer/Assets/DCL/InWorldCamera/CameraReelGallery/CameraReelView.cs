using DCL.InWorldCamera.CameraReelGallery.Components;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelView : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] internal Button goToCameraButton;

        [Header("Storage objects")]
        [SerializeField] internal StorageProgressBar storageProgressBar;
        [SerializeField] internal GameObject storageFullIcon;
        [SerializeField] internal GameObject loadingSpinner;
        [SerializeField] internal GameObject emptyState;
        [SerializeField] internal CanvasGroup storageFullToast;

        [Header("Gallery")]
        public CameraReelGalleryView cameraReelGalleryView;

        [Header("Storage configuration")]
        [SerializeField] internal float storageFullToastFadeTime = 0.3f;

        [Header("Context menu")]
        public OptionButtonView optionsButton;
        public ContextMenuView contextMenu;

        [Header("Animators")]
        [SerializeField] internal Animator panelAnimator;
        [SerializeField] internal Animator headerAnimator;

        internal event Action? MouseEnter;
        internal event Action? MouseExit;

        public void OnPointerEnter() =>
            MouseEnter?.Invoke();
        public void OnPointerExit() =>
            MouseExit?.Invoke();

        private void Awake()
        {
            storageFullIcon.SetActive(false);
            storageFullToast.alpha = 0;
        }
    }
}
