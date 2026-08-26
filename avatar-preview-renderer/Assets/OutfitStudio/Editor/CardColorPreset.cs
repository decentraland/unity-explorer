using UnityEngine;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// A named Card-frame skin: the card's inner/outer vignette pair, the border colour, the
    /// pattern texture tiled over the card, and the radial glow behind the avatar. Editor-only
    /// tooling asset: author them via
    /// <c>Assets ▸ Create ▸ Outfit Studio ▸ Card Color Preset</c> (or the "Save current…" button in
    /// the Outfit Studio's Card-frame section) and apply them with the preset buttons there.
    ///
    /// Only the paint is stored - margins, fade sizes, corner radius, border width and the
    /// enable/mask toggles are intentionally left untouched so a preset re-skins the current
    /// layout instead of overwriting it. Lives in the Editor assembly, so nothing ships to a build.
    ///
    /// 2026-07-30: was 7 colours (background top/bottom, glow, card top/bottom, border, bottom fade)
    /// back when the frame had its own gradient background and a flat card fill. The background layer
    /// is gone and the card is painted with the Decentraland vignette, so only the vignette pair and
    /// the border remain, plus <see cref="pattern"/> — see IMPLEMENTATION.md §18.
    ///
    /// 2026-08-25: the glow came back as <see cref="glowColor"/>/<see cref="glowRadius"/>, this time
    /// as the card paint's own additive hotspot rather than a separate background layer. It had been
    /// fixed at the reference material's values, which made it impossible to get a dark card — the
    /// purple survived blacking out both vignette colours and turning the pattern off.
    /// </summary>
    [CreateAssetMenu(fileName = "CardColorPreset", menuName = "Outfit Studio/Card Color Preset")]
    public class CardColorPreset : ScriptableObject
    {
        // Defaults mirror StudioCardFrame.Def* so a fresh preset matches the stock look.
        [ColorUsage(false)] public Color cardInner = new(0.7490f, 0f, 1f, 1f);
        [ColorUsage(false)] public Color cardOuter = new(0.3020f, 0f, 0.5020f, 1f);
        [ColorUsage(false)] public Color border = new(1f, 0.5059f, 0.3451f, 1f); // #FF8158

        /// <summary>The card's pattern texture. Left empty (as it is on presets authored before this
        /// field existed) the preset applies the bundled <c>DclBackgroundPattern</c> — a preset always
        /// fully determines the look rather than half-inheriting the current one.</summary>
        public Texture2D pattern;

        /// <summary>Whether the pattern draws at all. Defaults true so presets authored before this
        /// field existed (2026-07-31) keep showing their pattern rather than silently going blank.</summary>
        public bool patternEnabled = true;

        /// <summary>The radial hotspot behind the avatar. Added on top of the vignette rather than
        /// blended into it, so it stays visible with <see cref="cardInner"/>/<see cref="cardOuter"/>
        /// black and the pattern off — which is exactly why it belongs in the preset. Alpha is the
        /// shader's strength multiplier, so alpha 0 is "no glow".</summary>
        [ColorUsage(true)] public Color glowColor = new(0.66f, 0f, 0.745f, 1f);

        /// <summary>The glow's (horizontal, vertical) radius in the card's 0..1 UV space. Stock is the
        /// narrow, tall ellipse from the reference material.</summary>
        public Vector2 glowRadius = new(0.05f, 0.13f);
    }
}
