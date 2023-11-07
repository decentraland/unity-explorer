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
}
