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
        private readonly LobbyScreenSubView subView;

        public LobbyAuthState(
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            AuthenticationScreenCharacterPreviewController characterPreviewController) : base(viewInstance)
        {
            subView = viewInstance.LobbyScreenSubView;
            this.controller = controller;
            this.currentState = currentState;
            this.characterPreviewController = characterPreviewController;

            profileNameLabel = (StringVariable)subView!.ProfileNameLabel.StringReference["back_profileName"];
        }

        public void Enter((Profile profile, bool isCached) payload)
        {
            currentState.Value = payload.isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;

            Profile? profile = payload.profile;

            profileNameLabel!.Value = IsNewUser() ? profile.Name : "back " + profile.Name;

            subView.JumpIntoWorldButton.gameObject.SetActive(true);
            subView.JumpIntoWorldButton.transform.parent.gameObject.SetActive(true);
            subView.JumpIntoWorldButton.interactable = true;

            subView.ProfileNameLabel.gameObject.SetActive(true);
            subView.Description.SetActive(true);
            subView.DiffAccountButton.SetActive(true);

            subView.gameObject.SetActive(true);

            subView.FinalizeAnimator.ResetAnimator();
            subView.FinalizeAnimator.SetTrigger(UIAnimationHashes.IN);

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
            subView.gameObject.SetActive(false);

            characterPreviewController?.OnHide();

            subView.JumpIntoWorldButton.onClick.RemoveListener(JumpIntoWorld);
        }

        private void JumpIntoWorld()
        {
            subView!.JumpIntoWorldButton.interactable = false;
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
