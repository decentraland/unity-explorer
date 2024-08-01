using Cysharp.Threading.Tasks;
using DCL.Character.CharacterMotion.Components;
using System;
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

        public void NotifyFinish()
        {
            splashScreenAnimation.SetTrigger(AnimationHashes.OUT);
        }

        public async UniTask ShowSplashAsync(CancellationToken ct)
        {
            splashRoot.SetActive(showSplash);
            await TryShowSplash(ct);
            splashScreenAnimation.transform.SetSiblingIndex(1);
        }

        public void HideSplash()
        {
            splashRoot.SetActive(false);
        }

        private UniTask TryShowSplash(CancellationToken ct)
        {
            return showSplash
                ? UniTask.WaitUntil(() => splashScreenAnimation.GetCurrentAnimatorStateInfo(0).normalizedTime > 1, cancellationToken: ct)
                : UniTask.CompletedTask;
        }
    }
}
