using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class FadeOutSprite : MonoBehaviour
    {
        public float duration;

        private void OnEnable()
        {
            Invoke(nameof(Disable), duration);
        }

        private void Disable()
        {
            DestroyImmediate(gameObject);
        }
    }
}
