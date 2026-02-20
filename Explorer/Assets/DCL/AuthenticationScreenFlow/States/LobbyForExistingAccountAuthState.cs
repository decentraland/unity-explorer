using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.PerformanceAndDiagnostics;
using DCL.Profiles;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow
{
    public class LobbyForExistingAccountAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached, CancellationToken ct)>
    {
        private readonly CharacterPreviewView characterPreviewView;
        private readonly MVCStateMachine<AuthStateBase> fsm;
        private readonly AuthenticationScreenController controller;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly StringVariable? profileNameLabel;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly LobbyForExistingAccountAuthView view;
        private readonly SplashScreen splashScreen;

        private readonly Vector3 characterPreviewOrigPosition;
        private CancellationToken loginCt;

        public LobbyForExistingAccountAuthState(MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            SplashScreen splashScreen,
            ReactiveProperty<AuthStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            view = viewInstance.LobbyForExistingAccountAuthView;
            this.fsm = fsm;
            this.controller = controller;
            this.splashScreen = splashScreen;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;

            characterPreviewView = viewInstance.CharacterPreviewView;
            characterPreviewOrigPosition = characterPreviewView.transform.localPosition;

            view.OnViewHidden += ReparentCharacterPreview;
            return;

            void ReparentCharacterPreview()
            {
                characterPreviewView.transform.SetParent(viewInstance.transform);
                characterPreviewView.transform.localPosition = characterPreviewOrigPosition;
            }
        }

        public void Enter((Profile profile, bool isCached, CancellationToken ct) payload)
        {
            base.Enter();

            loginCt = payload.ct;
            view.JumpIntoWorldButton.interactable = true;
            view.DiffAccountButton.interactable = true;

            // splashScreen is destroyed after the first login
            if (splashScreen != null)
                splashScreen.FadeOutAndHide();

            currentState.Value = payload.isCached ? AuthStatus.LoggedInCached : AuthStatus.LoggedIn;

            Profile? profile = payload.profile;

            view.Show(IsNewUser() ? profile.Name : "back " + profile.Name);

            characterPreviewView.transform.SetParent(view.transform);
            characterPreviewView.transform.SetAsFirstSibling();
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            characterPreviewController.Initialize(profile.Avatar, CharacterPreviewUtils.AUTH_SCREEN_PREVIEW_POSITION);
            characterPreviewController.OnBeforeShow();
            characterPreviewController.OnShow();

            // Listeners
            view.JumpIntoWorldButton.onClick.AddListener(OnJumpIntoWorld);
            view.DiffAccountButton.onClick.AddListener(OnDiffAccountButtonClicked);
            return;

            bool IsNewUser() =>
                profile.Version == 1;
        }

        public override void Exit()
        {
            characterPreviewController.OnHide();

            view.JumpIntoWorldButton.onClick.RemoveAllListeners();
            view.DiffAccountButton.onClick.RemoveAllListeners();

            loginCt = CancellationToken.None;
            base.Exit();
        }

        private void OnDiffAccountButtonClicked()
        {
            view.Hide(UIAnimationHashes.SLIDE);
            controller.ChangeAccount();
        }

        private void OnJumpIntoWorld()
        {
            view!.JumpIntoWorldButton.interactable = false;
            view!.DiffAccountButton.interactable = false;

            AnimateAndAwaitAsync(loginCt).Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync(CancellationToken ct)
            {
                try
                {
                    await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                    view.Hide(UIAnimationHashes.OUT);
                    await UniTask.Delay(ANIMATION_DELAY, cancellationToken: ct);
                    characterPreviewController?.OnHide();

                    fsm.Enter<InitAuthState>();
                    controller.TrySetLifeCycle();
                }
                catch (OperationCanceledException)
                { /* Expected on cancellation */
                }
            }
        }
    }
}
