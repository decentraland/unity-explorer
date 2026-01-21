using MVC;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class InitAuthState : AuthStateBase, IState
    {
        private readonly string buildDataInstallSource;

        /// <summary>
        /// Set main View  prefab to the default visual state in case it was forgotten to disable some gameObjects during prefab editing
        /// </summary>
        public InitAuthState(AuthenticationScreenView viewInstance, string buildDataInstallSource) : base(viewInstance)
        {
            this.buildDataInstallSource = buildDataInstallSource;
        }

        public void Enter()
        {
            viewInstance.LoginSelectionAuthView.gameObject.SetActive(false);

            viewInstance.VerificationDappAuthView.gameObject.SetActive(false);
            viewInstance.VerificationOTPAuthView.gameObject.SetActive(false);

            viewInstance.LobbyForExistingAccountAuthView.gameObject.SetActive(false);
            viewInstance.LobbyForNewAccountAuthView.gameObject.SetActive(false);

            viewInstance.ErrorPopupRoot.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.VersionText.text = Application.isEditor
                ? $"editor-version - {buildDataInstallSource}"
                : $"{Application.version} - {buildDataInstallSource}";
        }
    }
}
