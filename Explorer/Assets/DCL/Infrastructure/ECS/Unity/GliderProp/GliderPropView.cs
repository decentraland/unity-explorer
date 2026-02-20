using DCL.Audio.Avatar;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.GliderProp
{
    /// <summary>
    /// Controls all audio / visual aspects of the glider prop
    ///
    /// <see cref="UpdateEngineState"/> needs to be called to set whether the engine is enabled or not and the 'engine level'
    /// </summary>
    public class GliderPropView : MonoBehaviour
    {
        private bool trailEnabled;

        private float overallEngineVolume;

        private float smoothedEngineLevel;

        // How smoothly the 'engine level' is interpolated between idle and full speed
        // (see UpdateEngineState)
        [field: Header("General Settings")] [field: SerializeField] public float EngineLevelSmoothness { get; private set; } = 1;

        [field: Header("Animation")] [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public List<Transform> Rotors { get; private set; }
        [field: SerializeField] public Vector2 RotorRotationSpeedRange { get; private set; } = new (360, 360 * 4);

        [field: Header("Audio")] [field: SerializeField] public AvatarAudioSettings Settings { get; private set; }
        [field: SerializeField] public AudioClip OpenGliderClip { get; private set; }
        [field: SerializeField] public AudioClip CloseGliderClip { get; private set; }
        [field: SerializeField] public AudioSource IdleAudioSource { get; private set; }
        [field: SerializeField] public AudioSource MovingAudioSource { get; private set; }
        [field: SerializeField] [field: Range(0, 1)] public float IdleMaxVolume { get; private set; } = 1;
        [field: SerializeField] [field: Range(0, 1)] public float FullSpeedMaxVolume { get; private set; } = 1;
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
            IdleAudioSource.priority = priority;
            MovingAudioSource.priority = priority;

            IdleAudioSource.volume = 0;
            MovingAudioSource.volume = 0;
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
            float normalizedEngineLevel = engineEnabled ? Mathf.Clamp01(engineLevel / FullSpeedEngineLevel) : 0;
            smoothedEngineLevel = Mathf.MoveTowards(smoothedEngineLevel, normalizedEngineLevel, dt / EngineLevelSmoothness);

            UpdateRotorsAnimation(dt);
            UpdateEngineVolume(engineEnabled, dt);
        }

        private void UpdateRotorsAnimation(float dt)
        {
            float rotationSpeed = Mathf.Lerp(RotorRotationSpeedRange.x, RotorRotationSpeedRange.y, smoothedEngineLevel);
            foreach (Transform rotor in Rotors) rotor.localRotation *= Quaternion.Euler(0, 0, rotationSpeed * dt);
        }

        private void UpdateEngineVolume(bool engineEnabled, float dt)
        {
            if (!Settings.AudioEnabled)
            {
                // Intended immediate cutoff if audio is disabled by the settings, no interpolation
                IdleAudioSource.volume = 0;
                MovingAudioSource.volume = 0;
                return;
            }

            // When the engine is disabled we want to stop playing the engine sounds
            // If we didn't have this additional multiplier we'd be playing the idle
            const float VOLUME_LERP_FACTOR = 4;
            overallEngineVolume = Mathf.MoveTowards(overallEngineVolume, engineEnabled ? 1 : 0, dt * VOLUME_LERP_FACTOR);

            IdleAudioSource.volume = (1 - smoothedEngineLevel) * IdleMaxVolume * overallEngineVolume;
            MovingAudioSource.volume = smoothedEngineLevel * FullSpeedMaxVolume * overallEngineVolume;
        }

        public void PrepareForNextActivation()
        {
            OpenAnimationCompleted = false;
            CloseAnimationCompleted = false;
        }

        #region Animation Events

        private void OnOpenAnimationStarted()
        {
            if (!Settings.AudioEnabled) return;

            AudioSource.PlayClipAtPoint(OpenGliderClip, transform.position);        }

        private void OnOpenAnimationCompleted() =>
            OpenAnimationCompleted = true;

        private void OnCloseAnimationStarted()
        {
            if (!Settings.AudioEnabled) return;

            AudioSource.PlayClipAtPoint(CloseGliderClip, transform.position);
        }

        private void OnCloseAnimationCompleted() =>
            CloseAnimationCompleted = true;

        #endregion
    }
}
