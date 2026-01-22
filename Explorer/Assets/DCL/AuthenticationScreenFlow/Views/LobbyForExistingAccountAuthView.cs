using Cysharp.Threading.Tasks;
using DCL.UI;
using DCL.Utility.Extensions;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
    [RequireComponent(typeof(Animator))]
    public class LobbyForExistingAccountAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;

        [Space]
        [SerializeField] private LocalizeStringEvent title;
        [SerializeField] private GameObject description;
        [SerializeField] private GameObject diffAccountButton;

        private StringVariable profileNameLabel => profileNameLabelField ??= (StringVariable)title.StringReference["back_profileName"];

        private StringVariable? profileNameLabelField;
        private int hideAnimHash = UIAnimationHashes.OUT;

        private void Awake()
        {
            title.gameObject.SetActive(true);
            description.SetActive(true);
            JumpIntoWorldButton.gameObject.SetActive(true);
            diffAccountButton.SetActive(true);
        }

        public void Show(string profileName)
        {
            profileNameLabel.Value = profileName;
            JumpIntoWorldButton.interactable = true;

            ShowAsync(CancellationToken.None).Forget();
        }

        public void Hide(int animHash)
        {
            hideAnimHash = animHash;
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
