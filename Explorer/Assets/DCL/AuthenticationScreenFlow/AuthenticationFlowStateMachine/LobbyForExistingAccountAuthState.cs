using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyForExistingAccountAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached)>
    {
        private readonly CharacterPreviewView characterPreviewView;
        private readonly MVCStateMachine<AuthStateBase> fsm;
        private readonly AuthenticationScreenController controller;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly StringVariable? profileNameLabel;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly LobbyForExistingAccountAuthView view;
        private readonly SplashScreen splashScreen;

        private readonly Vector3 characterPreviewOrigPosition;

        public LobbyForExistingAccountAuthState(MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            SplashScreen splashScreen,
            ReactiveProperty<AuthenticationStatus> currentState,
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

        public void Enter((Profile profile, bool isCached) payload)
        {
            view.JumpIntoWorldButton.interactable = true;

            // splashScreen is destroyed after the first login
            if (splashScreen != null)
                splashScreen.FadeOutAndHide();

            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            Profile? profile = payload.profile;

            view.Show(IsNewUser() ? profile.Name : "back " + profile.Name);

            characterPreviewView.transform.SetParent(view.transform);
            characterPreviewView.transform.localPosition = characterPreviewOrigPosition;

            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            // Listeners
            view.JumpIntoWorldButton.onClick.AddListener(OnJumpIntoWorld);
            view.DiffAccountButton.onClick.AddListener(OnDiffAccountButtonClicked);
            return;

            bool IsNewUser() =>
                profile.Version == 1;
        }

        public override void Exit()
        {
            characterPreviewController?.OnHide();

            view.JumpIntoWorldButton.onClick.RemoveAllListeners();
            view.DiffAccountButton.onClick.RemoveAllListeners();
        }

        private void OnDiffAccountButtonClicked()
        {
            view.Hide(UIAnimationHashes.SLIDE);
            controller.RestartLogin(enterLoginState: true);
        }

        private void OnJumpIntoWorld()
        {
            view!.JumpIntoWorldButton.interactable = false;

            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                view.Hide(UIAnimationHashes.OUT);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                fsm.Enter<InitAuthState>();
                controller.TrySetLifeCycle();
            }
        }
    }
}
