using DCL.Audio.Avatar;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GliderProp
{
    public class GliderPropView : MonoBehaviour
    {
        private bool trailEnabled;

        // Overall multiplier applied to engine sounds
        private float engineVolume;

        [field: Header("Animation")] [field: SerializeField] public Animator Animator { get; private set; }

        [field: Header("Audio")] [field: SerializeField] public AvatarAudioSettings Settings { get; private set; }
        [field: SerializeField] public AudioSourceSettings AudioSources { get; private set; }
        [field: SerializeField] public float IdleMaxVolume { get; private set; } = 1;
        [field: SerializeField] public float FullSpeedMaxVolume { get; private set; } = 1;
        [field: SerializeField] public float FullSpeedEngineLevel { get; private set; } = 1;

        [field: Header("VFX (Moving Only)")] [field: SerializeField] private List<ParticleSystem> ParticleSystems { get; set; }
        [field: SerializeField] private List<TrailRenderer> Trails { get; set; }

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

        private void Awake()
        {
            SetTrailRenderingEnabled(false);

            int priority = Settings.AudioPriority;
            AudioSources.OpenGlider.priority = priority;
            AudioSources.Idle.priority = priority;
            AudioSources.Moving.priority = priority;

            AudioSources.Idle.volume = 0;
            AudioSources.Moving.volume = 0;
        }

        private void SetTrailRenderingEnabled(bool value)
        {
            foreach (ParticleSystem particles in ParticleSystems)
                if (value)
                    particles.Play();
                else
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            foreach (TrailRenderer trail in Trails) trail.emitting = value;
        }

        public void SetEngineState(bool engineEnabled, float engineLevel, float dt)
        {
            const float VOLUME_TRANSITION_DURATION = 0.5f;
            engineVolume = Mathf.MoveTowards(engineVolume, engineEnabled ? 1 : 0, dt / VOLUME_TRANSITION_DURATION);

            if (!Settings.AudioEnabled)
            {
                AudioSources.Idle.volume = 0;
                AudioSources.Moving.volume = 0;
                return;
            }

            float t = Mathf.Clamp01(engineLevel / FullSpeedEngineLevel);
            AudioSources.Idle.volume = (1 - t) * IdleMaxVolume * engineVolume;
            AudioSources.Moving.volume = t * FullSpeedMaxVolume * engineVolume;
        }

        public void OnReturnedToPool()
        {
            OpenAnimationCompleted = false;
            CloseAnimationCompleted = false;
        }

#region Animation Events

        private void OnOpenAnimationStarted()
        {
            if (!Settings.AudioEnabled) return;

            AudioSources.OpenGlider.Play();
        }

        private void OnOpenAnimationCompleted() =>
            OpenAnimationCompleted = true;

        private void OnCloseAnimationCompleted() =>
            CloseAnimationCompleted = true;

#endregion

        [Serializable]
        public class AudioSourceSettings
        {
            [field: SerializeField] public AudioSource OpenGlider { get; private set; }

            [field: SerializeField] public AudioSource Idle { get; private set; }

            [field: SerializeField] public AudioSource Moving { get; private set; }
        }
    }
}
