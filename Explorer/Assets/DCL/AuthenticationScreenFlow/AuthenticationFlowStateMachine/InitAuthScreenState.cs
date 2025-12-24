using DCL.PerformanceAndDiagnostics.Analytics;
using UnityEngine;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class InitAuthScreenState : AuthStateBase
    {
        private readonly BuildData buildData;

        public InitAuthScreenState(AuthenticationScreenView viewInstance, BuildData buildData) : base(viewInstance)
        {
            this.buildData = buildData;
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
                ? $"editor-version - {buildData.InstallSource}"
                : $"{Application.version} - {buildData.InstallSource}";
        }

        public override void Exit()
        {
            base.Exit();
        }
    }
}
