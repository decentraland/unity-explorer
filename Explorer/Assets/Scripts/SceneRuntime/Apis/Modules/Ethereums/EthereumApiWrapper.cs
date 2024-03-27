using Cysharp.Threading.Tasks;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.Ethereums
{
    public class EthereumApiWrapper : IDisposable
    {
        private readonly IEthereumApi ethereumApi;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private CancellationTokenSource sendCancellationToken;

        public EthereumApiWrapper(IEthereumApi ethereumApi, ISceneExceptionsHandler sceneExceptionsHandler, IWeb3IdentityCache web3IdentityCache, CancellationTokenSource sendCancellationToken)
        {
            this.ethereumApi = ethereumApi;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            this.web3IdentityCache = web3IdentityCache;
            this.sendCancellationToken = sendCancellationToken;
        }

        public void Dispose()
        {
            sendCancellationToken.SafeCancelAndDispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object TryPay(decimal amount, string currency, string toAddress)
        {
            //TODO no payments yet
            return new SendEthereumMessageResponse
                { jsonAnyResponse = "{}" };
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object? UserAddress() =>
            web3IdentityCache.Identity?.Address.ToString() ?? null;

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object SignMessage(string message)
        {
            using AuthChain chain = web3IdentityCache.Identity.EnsureNotNull().Sign(message);
            var entity = chain.Get(AuthLinkType.ECDSA_SIGNED_ENTITY);
            return new SignMessageResponse(entity);
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object SendAsync(double id, string method, string jsonParams)
        {
            async UniTask<SendEthereumMessageResponse> SendAndFormatAsync(double id, string method, object[] @params, CancellationToken ct)
            {
                try
                {
                    object result = await ethereumApi.SendAsync<object>(new EthApiRequest
                    {
                        method = method,
                        @params = @params,
                    }, ct);

                    return new SendEthereumMessageResponse
                    {
                        jsonAnyResponse = JsonConvert.SerializeObject(new SendEthereumMessageResponse.Payload
                        {
                            id = (long)id,
                            jsonrpc = "2.0",
                            result = result,
                        }),
                    };
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);

                    return new SendEthereumMessageResponse
                    {
                        jsonAnyResponse = JsonConvert.SerializeObject(new SendEthereumMessageResponse.Payload
                        {
                            id = (long)id,
                            jsonrpc = "2.0",
                            result = null,
                        }),
                    };
                }
            }

            // TODO: support cancellations by id (?)
            sendCancellationToken = sendCancellationToken.SafeRestart();

            return SendAndFormatAsync(id, method, JsonConvert.DeserializeObject<object[]>(jsonParams), sendCancellationToken.Token)
                  .AsTask()
                  .ToPromise();
        }
    }
}
