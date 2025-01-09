using DCL.WebRequests.Analytics;
using UnityEngine;

namespace DCL.WebRequests
{
    [CreateAssetMenu(fileName = "Web Artificial Delay", menuName = "DCL/Various/Web Artificial Delay")]
    public class ArtificialDelayOptions : ScriptableObject
    {
        [SerializeField] private bool use;
        [SerializeField] private float delaySeconds;

        [ContextMenu(nameof(Upload))]
        public void Upload()
        {
            WebRequestsContainer.ElementBindingOptions options = new ();
            use = options.Enable.Value;
            delaySeconds = options.Delay.Value;
        }

        [ContextMenu(nameof(Flush))]
        public void Flush()
        {
            new WebRequestsContainer.ElementBindingOptions().ApplyValues(use, delaySeconds);
        }
    }
}
