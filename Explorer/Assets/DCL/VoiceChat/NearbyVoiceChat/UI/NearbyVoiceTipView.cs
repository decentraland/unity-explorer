using UnityEngine;
using UnityEngine.UI;

namespace DCL.VoiceChat.Nearby
{
    public class NearbyVoiceTipView : MonoBehaviour
    {
        [field: SerializeField] public Button CloseButton { get; private set; } = null!;
        [field: SerializeField] public Button TryItNowButton { get; private set; } = null!;

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
