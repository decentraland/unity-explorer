using Cysharp.Threading.Tasks;
using DCL.UI;
using MVC;
using Utility;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase, IState
    {
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly AuthenticationScreenController controller;

        public LobbyAuthState(AuthenticationScreenView viewInstance, AuthenticationScreenController controller,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            this.controller = controller;
            this.characterPreviewController = characterPreviewController;
        }

        public void Enter()
        {
            viewInstance.FinalizeContainer.SetActive(true);

            viewInstance.FinalizeAnimator.ResetAnimator();
            viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.JumpIntoWorldButton.interactable = true;

            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
        }

        public override void Exit()
        {
            viewInstance!.FinalizeContainer.SetActive(false);

            characterPreviewController?.OnHide();

            viewInstance.JumpIntoWorldButton.onClick.RemoveListener(JumpIntoWorld);
        }

        private void JumpIntoWorld()
        {
            viewInstance!.JumpIntoWorldButton.interactable = false;
            AnimateAndAwaitAsync().Forget();
            return;

            async UniTaskVoid AnimateAndAwaitAsync()
            {
                await (characterPreviewController?.PlayJumpInEmoteAndAwaitItAsync() ?? UniTask.CompletedTask);

                //Disabled animation until proper animation is setup, otherwise we get animation hash errors
                //viewInstance!.FinalizeAnimator.SetTrigger(UIAnimationHashes.JUMP_IN);
                await UniTask.Delay(AuthenticationScreenController.ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                controller.TrySetLifeCycle();
            }
        }
    }
}
