using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class RuntimeDeepLinkPlayground : MonoBehaviour
    {
        private void Start()
        {
#if !UNITY_WEBGL
            IDeepLinkHandle.Null.INSTANCE
                           .StartListenForDeepLinksAsync(destroyCancellationToken)
                           .Forget();
#endif
        }
    }
}
