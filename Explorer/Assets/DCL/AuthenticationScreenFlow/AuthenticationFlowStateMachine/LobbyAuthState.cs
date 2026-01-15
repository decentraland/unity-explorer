using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.UI;
using DCL.Utilities;
using MVC;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class LobbyAuthState : AuthStateBase, IPayloadedState<(Profile profile, bool isCached)>
    {
        private readonly AuthenticationScreenController controller;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly StringVariable? profileNameLabel;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;

        public LobbyAuthState(
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];
        }

        public void Enter((Profile profile, bool isCached) payload)
        {
            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            Profile? profile = payload.profile;

            profileNameLabel!.Value = IsNewUser() ? profile.Name : "back " + profile.Name;

            viewInstance.JumpIntoWorldButton.gameObject.SetActive(true);
            viewInstance.JumpIntoWorldButton.transform.parent.gameObject.SetActive(true);
            viewInstance.JumpIntoWorldButton.interactable = true;

            viewInstance.ProfileNameLabel.gameObject.SetActive(true);
            viewInstance.Description.SetActive(true);
            viewInstance.DiffAccountButton.SetActive(true);

            viewInstance.FinalizeContainer.SetActive(true);

            viewInstance.FinalizeAnimator.ResetAnimator();
            viewInstance.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);


            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);
            characterPreviewController?.OnBeforeShow();
            characterPreviewController?.OnShow();

            viewInstance.JumpIntoWorldButton.onClick.AddListener(JumpIntoWorld);
            return;

            bool IsNewUser() =>
                profile.Version == 1;
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
                await UniTask.Delay(ANIMATION_DELAY);
                characterPreviewController?.OnHide();

                controller.TrySetLifeCycle();
            }
        }
    }
}
