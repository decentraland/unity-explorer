using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using MVC;
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

        public LobbyForExistingAccountAuthState(MVCStateMachine<AuthStateBase> fsm,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            SplashScreen splashScreen,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            view = viewInstance.LobbyForExistingAccountAuthView;
            characterPreviewView = viewInstance.CharacterPreviewView;
            this.fsm = fsm;
            this.controller = controller;
            this.splashScreen = splashScreen;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;
        }

        public void Enter((Profile profile, bool isCached) payload)
        {
            if (splashScreen.gameObject.activeSelf)
                splashScreen.FadeOutAndHide();

            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            Profile? profile = payload.profile;

            view.Show(IsNewUser() ? profile.Name : "back " + profile.Name);

            characterPreviewView.gameObject.SetActive(true);
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
            characterPreviewView.gameObject.SetActive(false);
            characterPreviewController?.OnHide();

            // Listeners
            view.JumpIntoWorldButton.onClick.RemoveAllListeners();
            view.DiffAccountButton.onClick.RemoveAllListeners();
            ;
        }

        private void OnDiffAccountButtonClicked()
        {
            view.Hide(UIAnimationHashes.SLIDE);
            controller.ChangeAccount();
        }

        private void OnJumpIntoWorld()
        {
            view!.JumpIntoWorldButton.interactable = false;
            view.Hide(UIAnimationHashes.OUT);
            fsm.Enter<InitAuthState>();

            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {

                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);
                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                controller.TrySetLifeCycle();
            }
        }
    }
}
