using DG.Tweening;
using UnityEngine;

namespace DCL.Multiplayer.Movement.MessageBusMock
{
    public class FadeOutSprite : MonoBehaviour
    {
        public float duration;
        private Material mat;

        private void Awake()
        {
            mat = GetComponent<SpriteRenderer>().material;
        }

        private void OnEnable()
        {
            mat.DOFade(0.3f, duration).onComplete += Disable;
        }

        private void Disable()
        {
            DestroyImmediate(gameObject);
        }
    }
}
