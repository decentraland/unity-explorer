using DCL.Character.CharacterMotion.Components;
using TMPro;
using UnityEngine;

namespace DCL.SceneLoadingScreens.SplashScreen
{
    public class SplashScreen : ISplashScreen
    {
        private readonly Animator splashScreenAnimation;
        private readonly GameObject splashRoot;
        private readonly TMP_Text text;
        private readonly bool showSplash;

        public SplashScreen(Animator splashScreenAnimation, GameObject splashRoot, bool showSplash, TMP_Text text)
        {
            this.splashScreenAnimation = splashScreenAnimation;
            this.splashRoot = splashRoot;
            this.showSplash = showSplash;
            this.text = text;
            RemoveText();
        }

        public void Show(string? message = null)
        {
            splashRoot.SetActive(showSplash);
            splashScreenAnimation.transform.SetSiblingIndex(1);
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, true);

            if (message != null)
                PutText(message);
        }

        public void Hide()
        {
            splashScreenAnimation.SetBool(AnimationHashes.ENABLE, false);
            RemoveText();
        }

        private void PutText(string message)
        {
            text.text = message;
        }

        private void RemoveText()
        {
            text.text = string.Empty;
        }
    }
}
