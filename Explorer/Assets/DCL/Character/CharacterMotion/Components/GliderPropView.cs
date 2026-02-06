using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public class GliderPropView : MonoBehaviour
    {
        [field: SerializeField] private List<ParticleSystem> particleSystems;

        [field: SerializeField] private List<TrailRenderer> trails;

        private bool trailEnabled;

        [field: SerializeField] public Animator Animator { get; private set; }

        // Flags driven by animation events
        public bool OpenAnimationCompleted { get; private set; }
        public bool CloseAnimationCompleted { get; private set; }

        public bool TrailEnabled
        {
            get => trailEnabled;
            set
            {
                if (trailEnabled == value) return;

                trailEnabled = value;

                SetTrailRenderingEnabled(value);
            }
        }

        private void Awake() =>
            SetTrailRenderingEnabled(false);

        private void SetTrailRenderingEnabled(bool value)
        {
            foreach (ParticleSystem particles in particleSystems)
                if (value)
                    particles.Play();
                else
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            foreach (TrailRenderer trail in trails) trail.emitting = value;
        }

        private void OnOpenAnimationCompleted() =>
            OpenAnimationCompleted = true;

        private void OnCloseAnimationCompleted() =>
            CloseAnimationCompleted = true;

        public void OnReturnedToPool()
        {
            OpenAnimationCompleted = false;
            CloseAnimationCompleted = false;
        }
    }
}
