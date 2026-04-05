using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    /// Drives the destination marker animation sequence:
    /// plays the "Start" animation once, then loops "Idle".
    /// Also cycles the emission colour through a full hue rainbow.
    /// The AnimatorController is expected to be pre-assigned on the prefab's Animator.
    /// </summary>
    public class DestinationMarkerView : MonoBehaviour
    {
        private static readonly int START_STATE = Animator.StringToHash("Start");
        private static readonly int EMISSION_COLOR = Shader.PropertyToID("_EmissionColor");

        [field: SerializeField] public float RainbowSpeed { get; private set; } = 1f;
        [field: SerializeField] public float EmissionIntensity { get; private set; } = 2f;

        private Animator animator;
        private Renderer[] renderers;
        private float hue;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void ResetMarker()
        {
            hue = 0f;

            if (animator != null)
                animator.Play(START_STATE, 0, 0f);
        }

        private void Update()
        {
            UpdateRainbow();
        }

        private void UpdateRainbow()
        {
            hue = (hue + Time.deltaTime * RainbowSpeed) % 1f;
            Color emission = Color.HSVToRGB(hue, 1f, 1f) * EmissionIntensity;

            foreach (Renderer r in renderers)
                r.material.SetColor(EMISSION_COLOR, emission);
        }
    }
}
