using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    [RequireComponent(typeof(Image))]
    public class ImageView : MonoBehaviour
    {
        [field: SerializeField]
        internal GameObject LoadingObject { get; private set; }

        [field: SerializeField]
        internal Image Image { get; private set; }

        [field: SerializeField]
        internal float imageLoadingFadeDuration { get; private set; } = 0.3f;

        public Sprite ImageSprite => Image.sprite;

        [SerializeField] private AspectRatioFitter? aspectRatioFitter;
        [SerializeField] private RectTransform? rectTransform;

        private Vector2 originalPivot;
        private bool originalPreserveAspect;
        private readonly Vector2 centerPivot = new (0.5f, 0.5f);

        private void Awake()
        {
            originalPreserveAspect = Image.preserveAspect;

            if (rectTransform != null)
                originalPivot = rectTransform.pivot;
        }

        public bool IsLoading
        {
            get => LoadingObject.activeSelf;
            set => LoadingObject.SetActive(value);
        }

        public bool ImageEnabled
        {
            set => Image.enabled = value;
        }

        public float Alpha
        {
            set
            {
                Color color = Image.color;
                Image.color = new Color(color.r, color.g, color.b, value);
            }
        }

        public void SetImage(Sprite sprite, bool fitAndCenterImage = false)
        {
            Image.enabled = true;
            Image.sprite = sprite;
            LoadingObject.SetActive(false);

            //Cannot fit image if aspect ratio fitter or rect transform is not set
            if (aspectRatioFitter == null || rectTransform == null)
                return;

            rectTransform.pivot = fitAndCenterImage ? centerPivot : originalPivot;
            aspectRatioFitter.aspectMode = fitAndCenterImage ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.None;
            Image.preserveAspect = !fitAndCenterImage && originalPreserveAspect;

            if (sprite != null && sprite.texture != null)
                aspectRatioFitter.aspectRatio = fitAndCenterImage ? sprite.texture.width * 1f / sprite.texture.height : 1f;
        }

        public void SetColor(Color color) =>
            Image.color = color;

        public async UniTask FadeInAsync(float duration, CancellationToken ct)
        {
            Color color = Image.color;
            float t = 0f;
            float from = color.a;

            while (t < duration)
            {
                ct.ThrowIfCancellationRequested();
                Alpha = Mathf.Lerp(from, 1f, t / duration);
                t += Time.deltaTime;
                await UniTask.NextFrame(ct);
            }

            Alpha = 1f;
        }

        public void ShowImageAnimated()
        {
            Image.DOColor(Color.white, imageLoadingFadeDuration);
        }
    }
}
