using DCL.InWorldCamera.CameraReelGallery.Components;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelView : MonoBehaviour
    {
        [field: Header("Controls")]
        [field: SerializeField] internal Button goToCameraButton { get; private set; }

        [field: Header("Storage objects")]
        [field: SerializeField] internal StorageProgressBar storageProgressBar { get; private set; }
        [field: SerializeField] internal GameObject storageFullIcon { get; private set; }
        [field: SerializeField] internal GameObject emptyState { get; private set; }
        [field: SerializeField] internal CanvasGroup storageFullToast { get; private set; }

        [field: Header("Gallery")]
        [field: SerializeField] public CameraReelGalleryView CameraReelGalleryView { get; private set; }

        [field: Header("Storage configuration")]
        [field: SerializeField] internal float storageFullToastFadeTime { get; private set; } = 0.3f;

        [field: Header("Context menu")]
        [field: SerializeField] public OptionButtonView optionsButton { get; private set; }
        [field: SerializeField] public ContextMenuView contextMenu { get; private set; }

        [field: Header("Animators")]
        [field: SerializeField] internal Animator panelAnimator { get; private set; }
        [field: SerializeField] internal Animator headerAnimator { get; private set; }

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
