using System;
using UnityEngine;

namespace OutfitStudio
{
    /// <summary>
    /// Rotates the avatar a deterministic full 360° over <see cref="Duration"/> seconds,
    /// then restores the original rotation. Used by the Outfit Studio for turntable captures.
    /// Temporarily disables the <see cref="DragRotator"/> on the same object so the two
    /// don't fight over the transform.
    /// </summary>
    public class TurntableDriver : MonoBehaviour
    {
        public float Duration = 6f;
        public event Action Completed;

        private float _elapsed;
        private Quaternion _startRotation;
        private DragRotator _dragRotator;

        private void OnEnable()
        {
            _elapsed = 0f;
            _startRotation = transform.rotation;
            _dragRotator = GetComponent<DragRotator>();
            if (_dragRotator != null) _dragRotator.enabled = false;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / Mathf.Max(0.1f, Duration));

            transform.rotation = Quaternion.AngleAxis(360f * t, Vector3.up) * _startRotation;

            if (t >= 1f)
            {
                transform.rotation = _startRotation;
                if (_dragRotator != null) _dragRotator.enabled = true;

                var callback = Completed;
                Completed = null;
                enabled = false;

                callback?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (_dragRotator != null) _dragRotator.enabled = true;
        }
    }
}
