using DCL.InWorldCamera.CameraReel.Components;
using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelGalleryView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] internal RectTransform scrollContentRect;
        [SerializeField] internal ScrollRect scrollRect;
        [SerializeField] internal RectTransform elementMask;
        [SerializeField] internal CanvasGroup deleteReelModal;
        [SerializeField] internal Button deleteReelButton;
        [SerializeField] internal Button cancelDeleteIntentButton;
        [SerializeField] internal Button cancelDeleteIntentBackgroundButton;
        [SerializeField] internal WarningNotificationView errorNotificationView;
        [SerializeField] internal WarningNotificationView successNotificationView;
        [SerializeField] internal ScrollDragHandler scrollRectDragHandler;
        [SerializeField] internal ScrollDragHandler scrollBarDragHandler;

        [Header("Configuration")]
        public int paginationLimit = 100;
        [SerializeField] internal float loadMoreScrollThreshold = 0.4f;
        [SerializeField] internal float errorSuccessToastDuration = 3f;
        [SerializeField] internal float deleteModalAnimationDuration = 0.3f;

        [Header("Thumbnail objects")]
        [SerializeField] internal ReelThumbnailView thumbnailViewPrefab;
        [SerializeField] internal GameObject unusedThumbnailViewObject;

        [Header("Grid objects")]
        [SerializeField] internal MonthGridView monthGridPrefab;
        [SerializeField] internal GameObject unusedGridViewObject;

        internal event Action Disable;

        private void OnDisable() => Disable?.Invoke();

    }

}
