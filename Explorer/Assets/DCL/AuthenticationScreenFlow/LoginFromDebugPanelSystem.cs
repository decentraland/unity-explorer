using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Web3Authentication;
using ECS;
using ECS.Abstract;
using MVC;
using System.Threading;

namespace DCL.AuthenticationScreenFlow
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AUTHENTICATION)]
    public partial class LoginFromDebugPanelSystem : BaseUnityLoopSystem
    {
        private readonly IWeb3VerifiedAuthenticator web3Authenticator;
        private readonly MVCManager mvcManager;
        private readonly IRealmData realmData;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly DebugWidgetVisibilityBinding widgetVisibility;

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
                                 .AddSingleButton("Open Auth UI", OpenAuthenticationFlow);
        }

        protected override void Update(float t)
        {
            widgetVisibility.SetVisible(realmData.Configured);
        }

        private void Login()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            web3Authenticator.LoginAsync(cancellationTokenSource.Token).Forget();
        }

        private void OpenAuthenticationFlow()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand()).Forget();
        }
    }
}
