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
        private static readonly int IDLE_STATE = Animator.StringToHash("Idle");
        private static readonly int EMISSION_COLOR = Shader.PropertyToID("_EmissionColor");

        [SerializeField] public float rainbowSpeed = 1f;
        [SerializeField] public float emissionIntensity = 2f;

        private Animator animator;
        private Renderer[] renderers;
        private bool transitionedToIdle;
        private float hue;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void ResetMarker()
        {
            transitionedToIdle = false;
            hue = 0f;

            if (animator != null)
                animator.Play(START_STATE, 0, 0f);
        }

        private void Update()
        {
            UpdateAnimation();
            UpdateRainbow();
        }

        private void UpdateAnimation()
        {
            if (transitionedToIdle || animator == null) return;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

            if (info.shortNameHash == START_STATE && info.normalizedTime >= 1f)
            {
                transitionedToIdle = true;
                animator.Play(IDLE_STATE, 0, 0f);
            }
        }

        private void UpdateRainbow()
        {
            hue = (hue + Time.deltaTime * rainbowSpeed) % 1f;
            Color emission = Color.HSVToRGB(hue, 1f, 1f) * emissionIntensity;

            foreach (Renderer r in renderers)
                r.material.SetColor(EMISSION_COLOR, emission);
        }
    }
}
