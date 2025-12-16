using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utility;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using MVC;
using System.Threading;
using UnityEngine;

namespace DCL.UI.DuplicateIdentityPopup
{
    public class DuplicateIdentityWindowController : ControllerBase<DuplicateIdentityWindowView>
    {
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public DuplicateIdentityWindowController(
            ViewFactoryMethod viewFactory,
            IWeb3Authenticator web3Authenticator,
            IWeb3IdentityCache web3IdentityCache) : base(viewFactory)
        {
            this.web3Authenticator = web3Authenticator;
            this.web3IdentityCache = web3IdentityCache;
        }

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public static ViewFactoryMethod CreateLazily(DuplicateIdentityWindowView prefab) =>
            () => Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            int index = await UniTask.WhenAny(
                viewInstance!.ExitButton.OnClickAsync(ct),
                viewInstance.RestartButton.OnClickAsync(ct)
            );

            if (index == 1)
                await LogoutAsync(ct);
            else
                ExitUtils.Exit();
        }

        private async UniTask LogoutAsync(CancellationToken ct)
        {
            if (web3IdentityCache.Identity == null)
            {
                ReportHub.LogError(ReportCategory.UI, "Cannot logout. Identity is null. Exiting the game instead");
                ExitUtils.Exit();
                return;
            }

            await web3Authenticator.LogoutAsync(ct);
        }
    }
}


