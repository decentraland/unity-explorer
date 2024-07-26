using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.MainUI
{
    public class HideShowView : MonoBehaviour
    {
        [field: SerializeField]
        internal LayoutElement layoutElement { get; private set; }
        [field: SerializeField]
        internal float showLayoutWidth { get; private set; }
        [field: SerializeField]
        internal float hideLayoutWidth { get; private set; }
        [field: SerializeField]
        internal float hideWaitTime { get; private set; }
        [field: SerializeField]
        internal float showWaitTime { get; private set; }
        [field: SerializeField]
        internal float animationTime { get; private set; }

        [field: SerializeField]
        internal OpenSidebarView openSidebarView { get; private set; }

        private bool waitingToShow;
        private bool waitingToHide;
        private bool showing;

        private CancellationTokenSource showCancellationTokenSource = new ();
        private CancellationTokenSource hideCancellationTokenSource = new ();

        private void Start()
        {
            showing = true;
            openSidebarView.EnterOpenArea += OnPointerEnter;
            openSidebarView.ExitOpenArea += OnPointerExit;
        }

        public void OnPointerEnter()
        {
            if (showing)
            {
                waitingToShow = false;
            }

            if (showCancellationTokenSource.IsCancellationRequested)
            {
                waitingToShow = false;
            }

            if (!showing && !waitingToShow)
            {
                waitingToShow = true;
                showCancellationTokenSource = showCancellationTokenSource.SafeRestart();
                WaitAndShow(showCancellationTokenSource.Token).Forget();
            }

            if (waitingToHide)
            {
                hideCancellationTokenSource.Cancel();
            }
        }

        public void OnPointerExit()
        {
            if (waitingToShow || showing)
            {
                showCancellationTokenSource.Cancel();
            }

            if (hideCancellationTokenSource.IsCancellationRequested)
            {
                waitingToHide = false;
            }

            if (!waitingToHide && showing)
            {
                waitingToHide = true;
                hideCancellationTokenSource = hideCancellationTokenSource.SafeRestart();
                WaitAndHide(hideCancellationTokenSource.Token).Forget();
            }
        }

        private async UniTask WaitAndHide(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(hideWaitTime), cancellationToken: ct);
            waitingToHide = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(hideLayoutWidth, ct);

            if (ct.IsCancellationRequested) return;
            showing = false;
        }

        private async UniTask WaitAndShow(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(showWaitTime), cancellationToken: ct);
            waitingToShow = false;
            if (ct.IsCancellationRequested) return;

            await AnimateWidthAsync(showLayoutWidth, ct);

            if (ct.IsCancellationRequested) return;
            showing = true;
        }


        private async UniTask AnimateWidthAsync(float width, CancellationToken ct)
        {
            float startWidth = layoutElement.preferredWidth;
            float endWidth = width;
            float elapsedTime = 0f;

            while (elapsedTime < animationTime)
            {
                if (ct.IsCancellationRequested)
                {
                    layoutElement.preferredWidth = startWidth;
                    return;
                }
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / animationTime);
                layoutElement.preferredWidth = Mathf.Lerp(startWidth, endWidth, t);
                await UniTask.Yield();
            }

            layoutElement.preferredWidth = endWidth;
        }

    }
}
