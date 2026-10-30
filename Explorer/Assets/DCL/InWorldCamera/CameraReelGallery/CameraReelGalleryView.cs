using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.InWorldCamera.CameraReelToast;
using SuperScrollView;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery
{
    public class CameraReelGalleryView : MonoBehaviour
    {
        [field: Header("References")]
        [field: SerializeField] internal ScrollRect scrollRect { get; private set; } = null!;
        [field: SerializeField] internal LoopListView2 loopList { get; private set; } = null!;
        [field: SerializeField] internal GameObject loadingSpinner { get; private set; } = null!;
        [field: SerializeField] internal GameObject emptyState { get; private set; } = null!;

        [field: Header("Nullable references")]
        [field: SerializeField] internal Button? deleteReelButton { get; private set; }
        [field: SerializeField] internal Button? cancelDeleteIntentButton { get; private set; }
        [field: SerializeField] internal Button? cancelDeleteIntentBackgroundButton { get; private set; }
        [field: SerializeField] internal CameraReelToastMessage? cameraReelToastMessage { get; private set; }
        [field: SerializeField] internal CanvasGroup? deleteReelModal { get; private set; }

        [field: Header("Configuration")]
        [field: SerializeField] public int PaginationLimit { get; private set; } = 100;
        [field: SerializeField] internal int loadMoreCounterThreshold { get; private set; } = 12;
        [field: SerializeField] internal float deleteModalAnimationDuration { get; private set; } = 0.3f;

        [field: Header("Layout")]
        [field: SerializeField] internal float paddingTop { get; private set; } = 30f;
        [field: SerializeField] internal float paddingBottom { get; private set; } = 30f;
        [field: SerializeField] internal float paddingLeft { get; private set; } = 30f;
        [field: SerializeField] internal float monthSpacing { get; private set; } = 20f;

        [field: Header("Thumbnail objects")]
        [field: SerializeField] internal ReelThumbnailView thumbnailViewPrefab { get; private set; } = null!;

        internal event Action? Disable;

        private void OnDisable()
        {
            Disable?.Invoke();
        }
    }
}
