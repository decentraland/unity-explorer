using UnityEngine;
using UnityEngine.UI;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen : MonoBehaviour
    {
        [SerializeField] private Sprite[] logoSprites;
        [SerializeField] private Image logoImage;
        [SerializeField] private Animator splashScreenAnimation;

        private int frame;
        private float timer;

        public void Show() =>
            gameObject.SetActive(true);

        public void Hide() =>
            gameObject.SetActive(false);

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
