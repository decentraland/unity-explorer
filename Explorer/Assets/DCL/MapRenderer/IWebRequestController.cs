using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

namespace DCLServices.MapRenderer
{

    public interface IWebRequestController
    {
        //TODO: use URLAddress instead of string
        UniTask<UnityWebRequest> GetTextureAsync(string url, CancellationToken cancellationToken);
    }

    public class WebRequestController : IWebRequestController
    {
        public async UniTask<UnityWebRequest> GetTextureAsync(string url, CancellationToken cancellationToken)
        {
            var request = UnityWebRequest.Get(url);
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            return request;
        }
    }
}
