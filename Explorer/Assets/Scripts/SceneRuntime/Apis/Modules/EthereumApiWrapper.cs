using Cysharp.Threading.Tasks;
using DCL.Web3;
using JetBrains.Annotations;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
using SceneRunner.Scene.ExceptionsHandling;
using System;
using System.Threading;
using Utility;

namespace SceneRuntime.Apis.Modules
{
    public class EthereumApiWrapper : IDisposable
    {
        private readonly IEthereumApi ethereumApi;
        private readonly ISceneExceptionsHandler sceneExceptionsHandler;

        private CancellationTokenSource sendCancellationToken;

        public EthereumApiWrapper(IEthereumApi ethereumApi, ISceneExceptionsHandler sceneExceptionsHandler)
        {
            this.ethereumApi = ethereumApi;
            this.sceneExceptionsHandler = sceneExceptionsHandler;
        }

        public void Dispose()
        {
            sendCancellationToken?.SafeCancelAndDispose();
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/EthereumController.js")]
        public object SendAsync(double id, string method, string jsonParams)
        {
            async UniTask<SendEthereumMessageResponse> SendAndFormatAsync(string method, object[] @params, CancellationToken ct)
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
                        jsonAnyResponse = JsonConvert.SerializeObject(result),
                    };
                }
                catch (Exception e)
                {
                    sceneExceptionsHandler.OnEngineException(e);

                    return new SendEthereumMessageResponse
                    {
                        jsonAnyResponse = "",
                    };
                }
            }

            // TODO: support cancellations by id (?)
            sendCancellationToken = sendCancellationToken.SafeRestart();

            return SendAndFormatAsync(method, JsonConvert.DeserializeObject<object[]>(jsonParams), sendCancellationToken.Token)
                  .AsTask()
                  .ToPromise();
        }
    }
}
