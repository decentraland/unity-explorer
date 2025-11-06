using System.Threading.Tasks;
using UnityEngine.Networking;

namespace ThirdWebUnity.Playground
{
    public static class UnityWebRequestExtensions
    {
        public static Task SendWebRequestAsync(this UnityWebRequest request)
        {
            var tcs = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ => tcs.SetResult(true);
            return tcs.Task;
        }
    }
}
