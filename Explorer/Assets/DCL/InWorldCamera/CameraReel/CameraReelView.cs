using DCL.InWorldCamera.CameraReel.Components;
using System;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel
{
    public class CameraReelView : MonoBehaviour
    {
        [Header("Storage")]
        [SerializeField] internal StorageProgressBar storageProgressBar;
        [SerializeField] internal GameObject storageFullIcon;
        [SerializeField] internal CanvasGroup storageFullToast;
        [SerializeField] internal float storageFullToastFadeTime = 0.3f;

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
