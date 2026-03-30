using Cysharp.Threading.Tasks;
using SceneRuntime.ScenePermissions;
using System.Threading;

namespace DCL.Web3
{
    public class RestrictedEthereumApi : IEthereumApi
    {
        private readonly IEthereumApi impl;
        private readonly IJsApiPermissionsProvider jsApiPermissionsProvider;

        public RestrictedEthereumApi(IEthereumApi impl, IJsApiPermissionsProvider jsApiPermissionsProvider)
        {
            this.impl = impl;
            this.jsApiPermissionsProvider = jsApiPermissionsProvider;
        }

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct) =>
            SendAsync(request, Web3RequestSource.SDKScene, ct);

        public UniTask<EthApiResponse> SendAsync(EthApiRequest request, Web3RequestSource source, CancellationToken ct)
        {
            if (!jsApiPermissionsProvider.CanInvokeWeb3API())
                throw new Web3Exception("The Web3 API is not allowed");

            return impl.SendAsync(request, source, ct);
        }

        public void Dispose()
        {
            impl.Dispose();
        }
    }
}
