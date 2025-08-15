using UnityEngine;
using Utility.Animations;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen: MonoBehaviour
    {
        [SerializeField] private Animator splashScreenAnimation;

        public void Show()
        {
            gameObject.SetActive(true);
            splashScreenAnimation.transform.SetSiblingIndex(1);
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, false);
        }
    }
}
