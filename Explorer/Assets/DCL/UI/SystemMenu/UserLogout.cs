using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.UserInAppInitializationFlow;
using DCL.Web3;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System.Threading;

namespace DCL.UI.SystemMenu
{
    /// <summary>
    ///     Signs the current user out and takes the app back to the authentication screen.
    /// </summary>
    public class UserLogout
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly ICompositeWeb3Provider web3Authenticator;
        private readonly IProfileCache profileCache;
        private readonly IUserInAppInitializationFlow initializationFlow;
        private readonly World world;
        private readonly Entity playerEntity;

        public UserLogout(IWeb3IdentityCache identityCache,
            ICompositeWeb3Provider web3Authenticator,
            IProfileCache profileCache,
            IUserInAppInitializationFlow initializationFlow,
            World world,
            Entity playerEntity)
        {
            this.identityCache = identityCache;
            this.web3Authenticator = web3Authenticator;
            this.profileCache = profileCache;
            this.initializationFlow = initializationFlow;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public async UniTask LogoutAsync(CancellationToken ct)
        {
            if (identityCache.Identity == null)
            {
                ReportHub.LogError(ReportCategory.UI, "Cannot logout. Identity is null.");
                return;
            }

            Web3Address address = identityCache.Identity.Address;
            await web3Authenticator.LogoutAsync(ct);
            profileCache.Remove(address);

            await initializationFlow.ExecuteAsync(
                new UserInAppInitializationFlowParameters(
                    showAuthentication: true,
                    showLoading: true,
                    loadSource: IUserInAppInitializationFlow.LoadSource.Logout,
                    world: world,
                    playerEntity: playerEntity
                ),
                ct
            );
        }
    }
}
