using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class RuntimeDeepLinkPlayground : MonoBehaviour
    {
        private void Start()
        {
            DeepLinkSentinel
               .StartListenForDeepLinksAsync(
                    DeepLinkHandle.Null(),
                    destroyCancellationToken
                )
               .Forget();
        }
    }
}
