using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        [field: Header("References")]
        [field: SerializeField] internal RawImage thumbnailImage { get; private set; }
        [field: SerializeField] internal LoadingBrightView loadingBrightView { get; private set; }
        [field: SerializeField] internal RectTransform optionButtonContainer { get; private set; }
        [field: SerializeField] internal Button button { get; private set; }
        [field: SerializeField] internal GameObject outline { get; private set; }

        [field: Header("Configuration")]
        [field: SerializeField] internal Vector3 optionButtonOffset { get; private set; } = new (-11f, -23f, 0);
        [field: SerializeField] internal float scaleFactorOnHover { get; private set; } = 1.03f;
        [field: SerializeField] internal float scaleAnimationDuration { get; private set; } = 0.3f;
        [field: SerializeField] internal float thumbnailLoadedAnimationDuration { get; private set; } = 0.3f;

        internal event Action? PointerEnter;
        internal event Action? PointerExit;

        public void OnPointerEnter(PointerEventData eventData) =>
            PointerEnter?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            PointerExit?.Invoke();

        public void Dispose()
        {
            PointerEnter = null;
            PointerExit = null;
        }
    }
}
