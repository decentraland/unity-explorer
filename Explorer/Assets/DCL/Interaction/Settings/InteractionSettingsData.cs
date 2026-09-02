using UnityEngine;

namespace DCL.Interaction.Settings
{
    public class InteractionSettingsData : ScriptableObject
    {
        [field: SerializeField] public Color HighlightColor { get; private set; }

        /// <summary>
        ///     Outline width in pixels. Must stay above zero: the highlight shader reads a width of zero as
        ///     the interior draw and renders the surface effect instead of the outline.
        /// </summary>
        [field: SerializeField]
        [field: Min(0.01f)]
        public float Thickness { get; private set; }

        /// <summary>
        ///     Opacity applied across the whole visible surface, before the rim is added. This is what keeps a
        ///     large interactable readable when its outline falls outside the frustum.
        /// </summary>
        [field: Header("Surface effect")]
        [field: Space]
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float FresnelFill { get; private set; }

        /// <summary>
        ///     Opacity added at grazing angles, on top of <see cref="FresnelFill" />.
        /// </summary>
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float FresnelRim { get; private set; }

        /// <summary>
        ///     Falloff exponent of the rim. Higher values tighten it against the silhouette.
        /// </summary>
        [field: SerializeField]
        [field: Range(0.1f, 8f)]
        public float FresnelPower { get; private set; }

        /// <summary>
        ///     Ceiling on the combined fill and rim opacity, so the highlight always tints the object rather
        ///     than replacing it. Thin geometry lies inside the rim band along its whole width and would
        ///     otherwise render fully opaque.
        /// </summary>
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float MaxSurfaceOpacity { get; private set; }

        /// <summary>
        ///     Tolerance in metres when hiding the surface effect behind nearer geometry. Too low reads as
        ///     speckling on the highlighted surface itself.
        /// </summary>
        [field: SerializeField]
        [field: Range(0.001f, 0.5f)]
        public float SurfaceDepthBias { get; private set; }

        /// <summary>
        ///     Tolerance in metres when hiding the outline behind nearer geometry. Deliberately far looser
        ///     than <see cref="SurfaceDepthBias" />: the outline is drawn outside the silhouette, over ground
        ///     and walls whose depth differs from the object's own, and a tight value eats into the line.
        /// </summary>
        [field: SerializeField]
        [field: Range(0.01f, 2f)]
        public float OutlineDepthBias { get; private set; }

        /// <summary>
        ///     Pulse frequency in cycles per second.
        /// </summary>
        [field: Header("Pulse")]
        [field: Space]
        [field: SerializeField]
        [field: Range(0f, 5f)]
        public float PulseSpeed { get; private set; }

        /// <summary>
        ///     Surface opacity multiplier at the bottom of the pulse.
        /// </summary>
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float PulseMin { get; private set; }

        /// <summary>
        ///     Surface opacity multiplier at the top of the pulse.
        /// </summary>
        [field: SerializeField]
        [field: Range(0f, 1f)]
        public float PulseMax { get; private set; }
    }
}
