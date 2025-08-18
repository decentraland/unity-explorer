using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen : MonoBehaviour
    {
        [SerializeField] private Sprite[] logoSprites;
        [SerializeField] private Image logoImage;
        [SerializeField] private Animator splashScreenAnimation;

        private int frame = 0;
        private float timer = 0f;

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void CleanupLogo()
        {
            // TODO
        }

        private void Update()
        {
            const float FPS = 8f;

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
