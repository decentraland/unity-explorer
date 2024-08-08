using DCL.Character.CharacterMotion.Components;
using System.Threading;
using UnityEngine;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen : ISplashScreen
    {
        private readonly Animator splashScreenAnimation;
        private readonly GameObject splashRoot;
        private readonly bool showSplash;

        public SplashScreen(Animator splashScreenAnimation, GameObject splashRoot, bool showSplash)
        {
            this.splashScreenAnimation = splashScreenAnimation;
            this.splashRoot = splashRoot;
            this.showSplash = showSplash;
        }

        public void ShowSplash()
        {
            splashRoot.SetActive(showSplash);
            splashScreenAnimation.transform.SetSiblingIndex(1);
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, true);
        }

        public void HideSplash()
        {
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, false);
        }
    }
}
