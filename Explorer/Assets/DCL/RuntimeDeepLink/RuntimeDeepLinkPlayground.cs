using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class RuntimeDeepLinkPlayground : MonoBehaviour
    {
        private void Start()
        {
            DeepLinkSentinel
               .StartListenForDeepLinksAsync(
                    new[] { IDeepLinkHandle.Null.INSTANCE },
                    destroyCancellationToken
                )
               .Forget();
        }
    }
}
