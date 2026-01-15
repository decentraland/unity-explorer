using MVC;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class InitAuthScreenState : AuthStateBase, IState
    {
        private readonly string buildDataInstallSource;

        /// <summary>
        /// Set main View  prefab to the default visual state in case it was forgotten to disable some container during prefab editing
        /// </summary>
        public InitAuthScreenState(AuthenticationScreenView viewInstance, string buildDataInstallSource) : base(viewInstance)
        {
            this.buildDataInstallSource = buildDataInstallSource;
        }

        public void Enter()
        {
            viewInstance.AuthLoginScreenView.gameObject.SetActive(false);
            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.VerificationOTPContainer.SetActive(false);

            // Finilize
            viewInstance.FinalizeContainer.SetActive(false);
            viewInstance.JumpIntoWorldButton.gameObject.SetActive(true);
            viewInstance.DiffAccountButton.SetActive(true);
            viewInstance.ProfileNameLabel.gameObject.SetActive(true);
            viewInstance.Description.SetActive(true);
            viewInstance.NewUserContainer.SetActive(false);

            viewInstance.ErrorPopupRoot.SetActive(false);
            viewInstance.RestrictedUserContainer.SetActive(false);

            viewInstance.CharacterPreviewView.gameObject.SetActive(true);

            viewInstance.VersionText.text = Application.isEditor
                ? $"editor-version - {buildDataInstallSource}"
                : $"{Application.version} - {buildDataInstallSource}";
        }
    }
}
