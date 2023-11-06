using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine.Networking;

namespace DCLServices.MapRenderer
{

    public interface IWebRequestController
    {
        UniTask<UnityWebRequest> GetTextureAsync(string url, CancellationToken cancellationToken);
    }
}
