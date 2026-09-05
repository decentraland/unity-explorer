using UnityEngine;

namespace DCL.RuntimeDeepLink
{
    public class RuntimeDeepLinkPlayground : MonoBehaviour
    {
        private void Start()
        {
            IDeepLinkHandle.Null.INSTANCE
                           .StartListenForDeepLinksAsync(destroyCancellationToken)
                           .Forget();
        }
    }
}
