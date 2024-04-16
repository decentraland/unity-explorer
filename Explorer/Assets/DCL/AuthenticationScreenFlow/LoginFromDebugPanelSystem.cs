using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Web3.Authenticators;
using ECS;
using ECS.Abstract;
using MVC;
using System.Threading;
using Utility;

namespace DCL.AuthenticationScreenFlow
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AUTHENTICATION)]
    public partial class LoginFromDebugPanelSystem : BaseUnityLoopSystem
    {
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly MVCManager mvcManager;
        private readonly IRealmData realmData;
        private readonly DebugWidgetVisibilityBinding widgetVisibility;
        private CancellationTokenSource? cancellationTokenSource;

        public LoginFromDebugPanelSystem(World world,
            IDebugContainerBuilder debugContainerBuilder,
            IWeb3VerifiedAuthenticator web3Authenticator,
            MVCManager mvcManager,
            IRealmData realmData)
            : base(world)
        {
            this.web3Authenticator = web3Authenticator;
            this.mvcManager = mvcManager;
            this.realmData = realmData;

            debugContainerBuilder.AddWidget("Web3 Authentication")
                                 .SetVisibilityBinding(widgetVisibility = new DebugWidgetVisibilityBinding(false))
                                 .AddSingleButton("Login", Login)
                                 .AddSingleButton("Open Auth UI", OpenAuthenticationFlow)
                                 .AddSingleButton("Logout", Logout);
        }

        protected override void Update(float t)
        {
            widgetVisibility.SetVisible(realmData.Configured);
        }

        private void Logout()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LogoutAsync(cancellationTokenSource.Token).Forget();
        }

        private void Login()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LoginAsync(cancellationTokenSource.Token).Forget();
        }

        private void OpenAuthenticationFlow()
        {
            cancellationTokenSource?.SafeCancelAndDispose();
            cancellationTokenSource = new CancellationTokenSource();
            mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand()).Forget();
        }
    }
}
