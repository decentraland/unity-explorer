using DCL.Audio.Avatar;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GliderProp
{
    public class GliderPropView : MonoBehaviour
    {
        private bool trailEnabled;

        // Angular speed of the animated rotors, degrees / sec
        private float rotorsSpeed;

        // Overall multiplier applied to engine sounds
        private float engineVolume;

        [field: Header("Animation")] [field: SerializeField] public Animator Animator { get; private set; }

        [field: SerializeField] public List<Transform> Rotors { get; private set; }

        [field: SerializeField] public Vector2 RotorRotationSpeedRange { get; private set; } = new (360, 360 * 4);

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

        public void UpdateEngineState(bool engineEnabled, float engineLevel, float dt)
        {
            float normalizedEngineLevel = Mathf.Clamp01(engineLevel / FullSpeedEngineLevel);

            UpdateRotorsAnimation(engineEnabled, normalizedEngineLevel, dt);
            UpdateEngineVolume(engineEnabled, normalizedEngineLevel, dt);
        }

        private void UpdateRotorsAnimation(bool engineEnabled, float normalizedEngineLevel, float dt)
        {
            const float ROTORS_LERP_FACTOR = 4;
            rotorsSpeed = Mathf.MoveTowards(rotorsSpeed, engineEnabled ? normalizedEngineLevel : 0, dt * ROTORS_LERP_FACTOR);
            float rotationSpeed = Mathf.Lerp(RotorRotationSpeedRange.x, RotorRotationSpeedRange.y, rotorsSpeed);
            foreach (Transform rotor in Rotors) rotor.localRotation *= Quaternion.Euler(0, 0, rotationSpeed * dt);
        }

        private void UpdateEngineVolume(bool engineEnabled, float normalizedEngineLevel, float dt)
        {
            const float VOLUME_LERP_FACTOR = 2;
            engineVolume = Mathf.MoveTowards(engineVolume, engineEnabled ? 1 : 0, dt * VOLUME_LERP_FACTOR);

            if (!Settings.AudioEnabled)
            {
                // Intended immediate cutoff, no lerping
                AudioSources.Idle.volume = 0;
                AudioSources.Moving.volume = 0;
                return;
            }

            AudioSources.Idle.volume = (1 - normalizedEngineLevel) * IdleMaxVolume * engineVolume;
            AudioSources.Moving.volume = normalizedEngineLevel * FullSpeedMaxVolume * engineVolume;
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
