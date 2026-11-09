using DCL.UI;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    /// <summary>
    ///     One thumbnail slot of a gallery row, driven by <see cref="ReelThumbnailController" />.
    /// </summary>
    public class ReelThumbnailView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [field: Header("References")]
        [field: SerializeField] internal RawImage thumbnailImage { get; private set; } = null!;
        [field: SerializeField] internal LoadingBrightView loadingBrightView { get; private set; } = null!;
        [field: SerializeField] internal RectTransform optionButtonContainer { get; private set; } = null!;
        [field: SerializeField] internal Button button { get; private set; } = null!;
        [field: SerializeField] internal GameObject outline { get; private set; } = null!;

        [field: Header("Configuration")]
        [field: SerializeField] internal Vector3 optionButtonOffset { get; private set; } = new (-11f, -23f, 0);
        [field: SerializeField] internal float scaleFactorOnHover { get; private set; } = 1.03f;
        [field: SerializeField] internal float scaleAnimationDuration { get; private set; } = 0.3f;
        [field: SerializeField] internal float thumbnailLoadedAnimationDuration { get; private set; } = 0.3f;

        internal event Action? PointerEntered;
        internal event Action? PointerExited;

        public void Resize(Vector2 cellSize) =>
            ((RectTransform)transform).sizeDelta = cellSize;

        public void ShowTexture(Texture texture, bool fadeIn)
        {
            Vector2 cell = ((RectTransform)transform).sizeDelta;
            float originalToSmallerRatio = texture.height * 1f / cell.y;
            float realWidth = originalToSmallerRatio * cell.x;
            float realWidthDiff = texture.width - realWidth;
            thumbnailImage.texture = texture;
            thumbnailImage.uvRect = new Rect(realWidthDiff / 2f / texture.width, 0, (texture.width - realWidthDiff) / texture.width, 1);

            if (fadeIn)
                thumbnailImage.DOFade(1f, thumbnailLoadedAnimationDuration);
        }

        public void SetHovered(bool hovered)
        {
            transform.DOScale(hovered ? Vector3.one * scaleFactorOnHover : Vector3.one, scaleAnimationDuration);
            outline.SetActive(hovered);
        }

        public void Clear()
        {
            thumbnailImage.DOKill();
            transform.DOKill();
            transform.localScale = Vector3.one;
            outline.SetActive(false);
            thumbnailImage.texture = null;
        }

        public void OnPointerEnter(PointerEventData eventData) =>
            PointerEntered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) =>
            PointerExited?.Invoke();
    }
}
