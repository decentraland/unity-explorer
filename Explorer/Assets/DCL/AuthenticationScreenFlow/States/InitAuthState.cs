using MVC;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow.States
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
            viewInstance.VersionText.text = Application.isEditor
                ? $"editor-version - {buildDataInstallSource}"
                : $"{Application.version} - {buildDataInstallSource}";

            viewInstance.CharacterPreviewView.gameObject.SetActive(false);

            // Screens
            {
                viewInstance.LoginSelectionAuthView.gameObject.SetActive(false);

                // Verification
                viewInstance.VerificationDappAuthView.gameObject.SetActive(false);
                viewInstance.VerificationOTPAuthView.gameObject.SetActive(false);

                // Lobby
                viewInstance.LobbyForExistingAccountAuthView.gameObject.SetActive(false);
                viewInstance.LobbyForNewAccountAuthView.gameObject.SetActive(false);
            }

            // Popups
            viewInstance.LoginSelectionAuthView.ErrorPopupRoot.SetActive(false);
            viewInstance.LoginSelectionAuthView.RestrictedUserContainer.SetActive(false);
        }
    }
}
