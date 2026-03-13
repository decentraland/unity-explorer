using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Utility.Extensions;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class ProfileFetchingAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public Button CancelButton { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;

        private int hideAnimHash = UIAnimationHashes.OUT;

        public void Show()
        {
            ShowAsync(CancellationToken.None).Forget();
        }

        public void Hide(int hideAnimHash)
        {
            this.hideAnimHash = hideAnimHash;
            HideAsync(CancellationToken.None).Forget();
        }

        public override async UniTask ShowAsync(CancellationToken ct)
        {
            await base.ShowAsync(ct);
            canvasGroup.interactable = true;
        }

        public override async UniTask HideAsync(CancellationToken ct, bool isInstant = false)
        {
            canvasGroup.interactable = false;
            await base.HideAsync(ct, isInstant);
        }

        protected override async UniTask PlayShowAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(UIAnimationHashes.IN, UIAnimationHashes.IN, ct: ct);

        protected override async UniTask PlayHideAnimationAsync(CancellationToken ct) =>
            await animator.PlayAndAwaitAsync(hideAnimHash, hideAnimHash, ct: ct);
    }
}
