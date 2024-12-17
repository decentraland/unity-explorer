using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelToast;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelGalleryView : MonoBehaviour
    {
        [field: Header("References")]
        [field: SerializeField] internal RectTransform scrollContentRect { get; private set; }
        [field: SerializeField] internal ScrollRect scrollRect { get; private set; }
        [field: SerializeField] internal RectTransform elementMask { get; private set; }
        [field: SerializeField] internal ScrollDragHandler scrollRectDragHandler { get; private set; }
        [field: SerializeField] internal ScrollDragHandler scrollBarDragHandler { get; private set; }
        [field: SerializeField] internal GameObject loadingSpinner { get; private set; }
        [field: SerializeField] internal GameObject emptyState { get; private set; }

        [field: Header("Nullable references")]
        [field: SerializeField] internal Button deleteReelButton { get; private set; }
        [field: SerializeField] internal Button cancelDeleteIntentButton { get; private set; }
        [field: SerializeField] internal Button cancelDeleteIntentBackgroundButton { get; private set; }
        [field: SerializeField] internal CameraReelToastMessage cameraReelToastMessage { get; private set; }
        [field: SerializeField] internal CanvasGroup deleteReelModal { get; private set; }

        [field: Header("Configuration")]
        [field: SerializeField] public int PaginationLimit { get; private set; } = 100;
        [field: SerializeField] internal int loadMoreCounterThreshold { get; private set; } = 12;
        [field: SerializeField] internal float deleteModalAnimationDuration { get; private set; } = 0.3f;

        [field: Header("Thumbnail objects")]
        [field: SerializeField] internal ReelThumbnailView thumbnailViewPrefab { get; private set; }
        [field: SerializeField] internal GameObject unusedThumbnailViewObject { get; private set; }

        [field: Header("Grid objects")]
        [field: SerializeField] internal MonthGridView monthGridPrefab { get; private set; }
        [field: SerializeField] internal GameObject unusedGridViewObject { get; private set; }

        internal event Action? Disable;

        private void OnDisable() => Disable?.Invoke();

    }

}
