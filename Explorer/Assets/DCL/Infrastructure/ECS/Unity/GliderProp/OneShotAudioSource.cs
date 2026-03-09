using DCL.Optimization.Pools;
using UnityEngine;

namespace ECS.Unity.GliderProp
{
    public class OneShotAudioSource : MonoBehaviour
    {
        private AudioSource audioSource;
        private GameObjectPool<OneShotAudioSource> pool;

        public void Initialize(GameObjectPool<OneShotAudioSource> pool)
        {
            audioSource = GetComponent<AudioSource>();
            this.pool = pool;
        }

        public void Play(AudioSource template, Vector3 position, int priority = -1)
        {
            CancelInvoke();
            transform.position = position;
            audioSource.clip = template.clip;
            audioSource.outputAudioMixerGroup = template.outputAudioMixerGroup;
            audioSource.volume = template.volume;
            audioSource.spatialBlend = template.spatialBlend;
            audioSource.priority = priority >= 0 ? priority : template.priority;
            audioSource.Play();
            Invoke(nameof(ReturnToPool), template.clip.length);
        }

        private void ReturnToPool()
        {
            audioSource.Stop();
            pool.Release(this);
        }
    }
}
