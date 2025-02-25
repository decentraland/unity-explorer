using Cysharp.Threading.Tasks;
using DCL.Web3;
using DCL.Web3.Identities;
using JetBrains.Annotations;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Text;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules.Ethereums
{
    public class EthereumApiWrapper : IJsApiWrapper
    {
        private readonly IEthereumApi ethereumApi;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly CancellationTokenSource sendCancellationToken;
        private CancellationTokenSource signMessageCancellationToken;

        public EthereumApiWrapper(IEthereumApi ethereumApi, ISceneExceptionsHandler sceneExceptionsHandler, IWeb3IdentityCache web3IdentityCache) : this(
            ethereumApi, sceneExceptionsHandler, web3IdentityCache, new CancellationTokenSource(), new CancellationTokenSource()
        ) { }

        public EthereumApiWrapper(IEthereumApi ethereumApi, ISceneExceptionsHandler sceneExceptionsHandler, IWeb3IdentityCache web3IdentityCache, CancellationTokenSource sendCancellationToken, CancellationTokenSource signMessageCancellationToken)
        {
            this.ethereumApi = ethereumApi;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
            this.web3IdentityCache = web3IdentityCache;
            this.sendCancellationToken = sendCancellationToken;
            this.signMessageCancellationToken = signMessageCancellationToken;
        }

        public void Dispose()
        {
            sendCancellationToken.SafeCancelAndDispose();
            signMessageCancellationToken.SafeCancelAndDispose();
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
            async UniTask<SignMessageResponse> RequestPersonalSignatureAsync(CancellationToken ct)
            {
                await UniTask.SwitchToMainThread();

                var hex = $"0x{Encoding.UTF8.GetBytes(message).ToHex()!}";

                try
                {
                    var response = await ethereumApi.SendAsync(new EthApiRequest
                    {
                        id = Guid.NewGuid().GetHashCode(),
                        method = "personal_sign",
                        @params = new object[]
                        {
                            hex,
                            web3IdentityCache.Identity!.Address.ToString(),
                        },
                    }, ct);

                    return new SignMessageResponse(hex, message, (string)response.result);
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);

                    // Returns empty signature in case of error
                    return new SignMessageResponse(hex, message, string.Empty);
                }
            }

            signMessageCancellationToken = signMessageCancellationToken.SafeRestart();

            return RequestPersonalSignatureAsync(signMessageCancellationToken.Token)
               .ToDisconnectedPromise();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object SendAsync(double id, string method, string jsonParams)
        {
            async UniTask<SendEthereumMessageResponse> SendAndFormatAsync(double id, string method, object[] @params, CancellationToken ct)
            {
                try
                {
                    var result = await ethereumApi.SendAsync(new EthApiRequest
                    {
                        id = (long)id,
                        method = method,
                        @params = @params,
                    }, ct);

                    return new SendEthereumMessageResponse
                    {
                        jsonAnyResponse = JsonConvert.SerializeObject(new SendEthereumMessageResponse.Payload
                        {
                            id = result.id,
                            jsonrpc = result.jsonrpc,
                            result = result.result,
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

            return SendAndFormatAsync(id, method, JsonConvert.DeserializeObject<object[]>(jsonParams) ?? Array.Empty<object>(), sendCancellationToken.Token)
               .ToDisconnectedPromise();
        }
    }
}
