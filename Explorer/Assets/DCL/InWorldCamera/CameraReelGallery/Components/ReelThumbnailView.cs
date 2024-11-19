using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        [Header("References")]
        public Image thumbnailImage;
        [SerializeField] internal LoadingBrightView loadingBrightView;
        [SerializeField] internal RectTransform optionButtonContainer;
        [SerializeField] internal Button button;
        [SerializeField] internal GameObject outline;

        [Header("Configuration")]
        [SerializeField] internal Vector3 optionButtonOffset = new (-15.83997f, -22f, 0);
        [SerializeField] internal float scaleFactorOnHover = 1.03f;
        [SerializeField] internal float scaleAnimationDuration = 0.3f;
        [SerializeField] internal float thumbnailLoadedAnimationDuration = 0.3f;

        internal event Action PointerEnter;
        internal event Action PointerExit;

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
