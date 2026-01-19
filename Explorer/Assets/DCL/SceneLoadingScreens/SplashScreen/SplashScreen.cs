using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen : MonoBehaviour
    {
        [SerializeField] private Sprite[] logoSprites;
        [SerializeField] private Image logoImage;
        [SerializeField] private Animator splashScreenAnimation;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeOutDuration = 1f;

        private int frame;
        private float timer;

        public void Show()
        {
            canvasGroup.alpha = 1;
            gameObject.SetActive(true);
        }

        public void Hide() =>
            gameObject.SetActive(false);

        public void FadeOutAndHide()
        {
            canvasGroup.DOFade(0f, fadeOutDuration)
                       .SetEase(Ease.Linear)
                       .OnComplete(Hide);
        }

        private void Update()
        {
            const float FPS = 30f;

            timer += Time.deltaTime;

            if (timer >= 1f / FPS)
            {
                timer = 0f;

                frame = (frame + 1) % logoSprites.Length;
                logoImage.sprite = logoSprites[frame];
            }
        }
    }
}
