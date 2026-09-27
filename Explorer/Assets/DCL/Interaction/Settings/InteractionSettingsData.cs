using UnityEngine;

namespace DCL.Interaction.Settings
{
    public class InteractionSettingsData : ScriptableObject
    {
        [SerializeField] private Color highlightColor;

        [SerializeField] [Min(0.01f)] private float thickness;

        [Header("Surface effect")]
        [Space]
        [SerializeField] [Range(0f, 1f)] private float fresnelFill;

        [SerializeField] [Range(0f, 1f)] private float fresnelRim;

        [SerializeField] [Range(0.1f, 8f)] private float fresnelPower;

        [SerializeField] [Range(0f, 1f)] private float maxSurfaceOpacity;

        [SerializeField] [Range(0.001f, 0.5f)] private float surfaceDepthBias;

        [SerializeField] [Range(0.01f, 2f)] private float outlineDepthBias;

        [Header("Pulse")]
        [Space]
        [SerializeField] [Range(0f, 5f)] private float pulseSpeed;

        [SerializeField] [Range(0f, 1f)] private float pulseMin;

        [SerializeField] [Range(0f, 1f)] private float pulseMax;

        public Color HighlightColor => highlightColor;

        /// <summary>
        ///     Outline width in pixels. Must stay above zero: the highlight shader reads a width of zero as
        ///     the interior draw and renders the surface effect instead of the outline.
        /// </summary>
        public float Thickness => thickness;

        /// <summary>
        ///     Opacity applied across the whole visible surface, before the rim is added. This is what keeps a
        ///     large interactable readable when its outline falls outside the frustum.
        /// </summary>
        public float FresnelFill => fresnelFill;

        /// <summary>
        ///     Opacity added at grazing angles, on top of <see cref="FresnelFill" />.
        /// </summary>
        public float FresnelRim => fresnelRim;

        /// <summary>
        ///     Falloff exponent of the rim. Higher values tighten it against the silhouette.
        /// </summary>
        public float FresnelPower => fresnelPower;

        /// <summary>
        ///     Ceiling on the combined fill and rim opacity, so the highlight always tints the object rather
        ///     than replacing it. Thin geometry lies inside the rim band along its whole width and would
        ///     otherwise render fully opaque.
        /// </summary>
        public float MaxSurfaceOpacity => maxSurfaceOpacity;

        /// <summary>
        ///     Tolerance in metres when hiding the surface effect behind nearer geometry. Too low reads as
        ///     speckling on the highlighted surface itself.
        /// </summary>
        public float SurfaceDepthBias => surfaceDepthBias;

        /// <summary>
        ///     Tolerance in metres when hiding the outline behind nearer geometry. Deliberately far looser
        ///     than <see cref="SurfaceDepthBias" />: the outline is drawn outside the silhouette, over ground
        ///     and walls whose depth differs from the object's own, and a tight value eats into the line.
        /// </summary>
        public float OutlineDepthBias => outlineDepthBias;

        /// <summary>
        ///     Pulse frequency in cycles per second.
        /// </summary>
        public float PulseSpeed => pulseSpeed;

        /// <summary>
        ///     Surface opacity multiplier at the bottom of the pulse.
        /// </summary>
        public float PulseMin => pulseMin;

        /// <summary>
        ///     Surface opacity multiplier at the top of the pulse.
        /// </summary>
        public float PulseMax => pulseMax;
    }
}
