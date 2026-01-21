using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.Utilities;
using MVC;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyForExistingAccountAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached)>
    {
        private readonly AuthenticationScreenController controller;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly StringVariable? profileNameLabel;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly ExistingAccountLobbyScreenSubView subView;

        public LobbyForExistingAccountAuthState(
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            subView = viewInstance.ExistingAccountLobbyScreenSubView;
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;

        }

        public void Enter((Profile profile, bool isCached) payload)
        {
            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            Profile? profile = payload.profile;

            subView.gameObject.SetActive(true);
            subView.ShowFor(IsNewUser() ? profile.Name : "back " + profile.Name);

            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            subView.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
            return;

            bool IsNewUser() =>
                profile.Version == 1;
        }

        public override void Exit()
        {
            subView.SlideBack();
            characterPreviewController?.OnHide();

            subView.JumpIntoWorldButton.onClick.RemoveListener(JumpIntoWorld);
        }

        private void JumpIntoWorld()
        {
            subView!.JumpIntoWorldButton.interactable = false;
            subView.FadeOut();

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
