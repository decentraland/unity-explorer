using UnityEngine;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class InitAuthScreenState : AuthStateBase
    {
        private readonly string buildDataInstallSource;

        /// <summary>
        /// Set main View  prefab to the default visual state in case it was forgotten to disable some container during prefab editing
        /// </summary>
        public InitAuthScreenState(AuthenticationScreenView viewInstance, string buildDataInstallSource) : base(viewInstance)
        {
            this.buildDataInstallSource = buildDataInstallSource;
        }

        public override void Enter()
        {
            base.Enter();
            viewInstance.LoginContainer.SetActive(false);
            viewInstance.VerificationContainer.SetActive(false);
            viewInstance.FinalizeContainer.SetActive(false);

            viewInstance.RestrictedUserContainer.SetActive(false);
            viewInstance.ErrorPopupRoot.SetActive(false);

            viewInstance.VersionText.text = Application.isEditor
                ? $"editor-version - {buildDataInstallSource}"
                : $"{Application.version} - {buildDataInstallSource}";
        }
    }
}
