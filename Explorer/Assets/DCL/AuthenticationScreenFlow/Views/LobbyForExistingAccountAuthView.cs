using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
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
    [RequireComponent(typeof(Animator), typeof(CanvasGroup))]
    public class LobbyForExistingAccountAuthView : ViewBase
    {
        [field: Space]
        [field: SerializeField]
        public Button JumpIntoWorldButton { get; private set; } = null!;
        [field: SerializeField]
        public Button DiffAccountButton { get; private set; } = null!;

        [Space]
        [SerializeField] private Animator animator = null!;
        [SerializeField] private CanvasGroup canvasGroup = null!;

        [Space]
        [SerializeField] private LocalizeStringEvent title = null!;
        [SerializeField] private GameObject description = null!;

        private const string PROFILE_NAME_VARIABLE = "back_profileName";

        private StringVariable? profileNameLabelField;
        private int hideAnimHash = UIAnimationHashes.OUT;

        private void Awake()
        {
            title.gameObject.SetActive(true);
            description.SetActive(true);
            JumpIntoWorldButton.gameObject.SetActive(true);
            DiffAccountButton.gameObject.SetActive(true);
        }

        public void Show(string profileName)
        {
            if (profileNameLabelField == null && title.StringReference.TryGetValue(PROFILE_NAME_VARIABLE, out IVariable variable))
                profileNameLabelField = (StringVariable)variable;

            if (profileNameLabelField != null)
                profileNameLabelField.Value = profileName;
            else
                ReportHub.LogError(ReportCategory.AUTHENTICATION, $"Localized title has no '{PROFILE_NAME_VARIABLE}' variable");

            JumpIntoWorldButton.interactable = true;
            DiffAccountButton.interactable = true;

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
