using DCL.InWorldCamera.CameraReel.Components;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelView : MonoBehaviour
    {
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

        internal event Action OnMouseEnter;
        internal event Action OnMouseExit;

        public void OnPointerEnter() =>
            OnMouseEnter?.Invoke();
        public void OnPointerExit() =>
            OnMouseExit?.Invoke();

        private void Awake()
        {
            storageFullIcon.SetActive(false);
            storageFullToast.alpha = 0;
        }
    }
}
