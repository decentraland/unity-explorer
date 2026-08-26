using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Composites a Fortnite-style "item card" frame around the previewed avatar, entirely inside
    /// the studio scene and captured for free (it's camera geometry, not a UI overlay — runtime UI
    /// overlays don't render through the capture camera; see IMPLEMENTATION.md §8/§18).
    ///
    /// Four camera-parented quads, ordered by render queue — no per-avatar depth *math* is needed, though
    /// two layers do depth-**test** so the avatar occludes them. There is deliberately NO background
    /// layer (2026-07-30): outside the card you see
    /// whatever the camera clears to — which this drives to transparent black — so a still exports
    /// with only the card and the avatar opaque.
    ///   • Card panel (queue 1500, ZWrite On) — rounded rect behind the avatar, painted with the
    ///     Decentraland vignette/pattern. The avatar (opaque, queue 2000) draws over it, so the head
    ///     overflowing the top edge is free. It's the only depth-writing layer, standing in for the
    ///     old background quad so a Skybox clear can't paint over the card.
    ///   • Side mask (queue 3200, ZTest Always, ZWrite On) — optional; ERASES to transparent outside the
    ///     card, and resets depth there so what it erased stops occluding the border.
    ///   • Bottom fade (queue 3500, ZTest Always, ZWrite On) — drawn after the avatar; fades the legs into
    ///     the card's own paint, clipped to the same rounded rect so its bottom corners match. Resets
    ///     depth over what it repaints, for the same reason as the mask.
    ///   • Border (queue 4000, ZTest LessEqual) — the ring. Last in the queue, so it wins over the fade
    ///     and the side mask, but depth-tested like the card, so the avatar occludes it (2026-08-06).
    ///
    /// Poll-based and studio-scene-gated like StudioAvatarShaderSwitcher / the pipeline switcher.
    /// The quads are HideFlags.DontSave (never serialized into the scene) and recreated after a
    /// domain reload or play-mode scene reload; nothing ships to a build.
    /// </summary>
    [InitializeOnLoad]
    public static class StudioCardFrame
    {
        private const string ROOT_NAME = "__OutfitStudio_CardFrame";
        private const string SHADER_NAME = "Custom/StudioCardFrame";
        private const float PLANE_Z = 50f; // camera-local Z; safely behind a ~2 m avatar, well inside the far plane
        // The side-mask quad spans the whole frame (it has to cover everything outside the card) and is
        // scaled slightly past the frustum so no edge sliver survives on an aspect mismatch. The card
        // rect is handed to it in this quad's UV space — see U() in PushParams().
        private const float MASK_OVERSIZE = 1.04f;

        // The border quad's own scale must exceed the card's by enough that the shader's mode-3 UV
        // remap (see StudioCardFrame.shader) has physical room to paint the outer ring past the card
        // edge, even at the slider's max width. Recomputed per Layout() from the card's own aspect —
        // see the comment there for the derivation.
        private const float MAX_BORDER_WIDTH = 0.2f; // matches both border-width shader Range(0,0.2) sliders
        private const float BORDER_OVERSIZE_MARGIN = 0.05f; // extra slack past the slider max for AA softening

        // The card's paint, ported from Explorer's loading screen; see the shader's DclCardPaint().
        private const string DCL_BG_TEXTURE_PATH = "Assets/OutfitStudio/Textures/DclBackgroundPattern.png";

        // EditorPrefs keys
        private const string K_ENABLED = "OutfitStudio.Card.Enabled";
        private const string K_DISABLE_MIDDLE_CARD = "OutfitStudio.Card.DisableMiddleCard";
        private const string K_DCL_INNER = "OutfitStudio.Card.DclInnerColor";
        private const string K_DCL_OUTER = "OutfitStudio.Card.DclOuterColor";
        private const string K_PATTERN = "OutfitStudio.Card.PatternTex"; // asset GUID, empty = bundled default
        private const string K_PATTERN_ENABLED = "OutfitStudio.Card.PatternEnabled";
        // The radial hotspot behind the avatar. Was fixed at the reference material's values until
        // 2026-08-25 — see GlowColor/GlowRadius.
        private const string K_GLOW_COLOR = "OutfitStudio.Card.GlowColor";
        private const string K_GLOW_RADIUS_X = "OutfitStudio.Card.GlowRadiusX";
        private const string K_GLOW_RADIUS_Y = "OutfitStudio.Card.GlowRadiusY";
        private const string K_SIDEMASK = "OutfitStudio.Card.SideMask";
        private const string K_BOTTOMMASK = "OutfitStudio.Card.BottomMask";
        private const string K_CLOSED_BORDER = "OutfitStudio.Card.ClosedBorder";
        // Replaced K_MARGIN_X ("OutfitStudio.Card.MarginX") on 2026-07-30: side margins were a fraction of
        // the frame WIDTH, which made the card change shape whenever the capture aspect changed. New key
        // because the meaning is different, not just the unit — see §20.
        private const string K_CARD_WIDTH = "OutfitStudio.Card.WidthFrameH";
        private const string K_MARGIN_TOP = "OutfitStudio.Card.MarginTop";
        private const string K_MARGIN_BOTTOM = "OutfitStudio.Card.MarginBottom";
        private const string K_BORDER = "OutfitStudio.Card.Border";
        private const string K_FADE_S = "OutfitStudio.Card.FadeSoftness";
        // These four changed UNIT on 2026-07-30 (card-relative → frame-height-relative, see §20), so they
        // get new key names: a stale value under the old key would be silently misread as the new unit and
        // quietly change someone's tuned card. Old keys are simply abandoned.
        private const string K_RADIUS = "OutfitStudio.Card.RadiusFrameH";
        private const string K_INNER_BORDER_W = "OutfitStudio.Card.InnerBorderWidthFrameH";
        private const string K_OUTER_BORDER_W = "OutfitStudio.Card.OuterBorderWidthFrameH";
        private const string K_FADE_H = "OutfitStudio.Card.FadeHeightFrameH";

        // Shader property ids
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int CardAspectId = Shader.PropertyToID("_CardAspect");
        private static readonly int CornerRadiusId = Shader.PropertyToID("_CornerRadius");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int InnerBorderWidthId = Shader.PropertyToID("_InnerBorderWidth");
        private static readonly int OuterBorderWidthId = Shader.PropertyToID("_OuterBorderWidth");
        private static readonly int BorderOversizeId = Shader.PropertyToID("_BorderOversize");
        private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        private static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
        private static readonly int MaskRectId = Shader.PropertyToID("_MaskRect");
        private static readonly int MaskTopOpenId = Shader.PropertyToID("_MaskTopOpen");
        private static readonly int BorderTopFadeId = Shader.PropertyToID("_BorderTopFade");
        private static readonly int DclOverlayTexId = Shader.PropertyToID("_DclOverlayTex");
        private static readonly int DclTileScaleId = Shader.PropertyToID("_DclTileScale");
        private static readonly int DclInnerColorId = Shader.PropertyToID("_DclInnerColor");
        private static readonly int DclOuterColorId = Shader.PropertyToID("_DclOuterColor");
        private static readonly int DclPatternEnabledId = Shader.PropertyToID("_DclPatternEnabled");
        private static readonly int DclGlowColorId = Shader.PropertyToID("_DclGlowColor");
        private static readonly int DclGlowRadiusId = Shader.PropertyToID("_DclGlowRadius");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SrcBlendAId = Shader.PropertyToID("_SrcBlendA");
        private static readonly int DstBlendAId = Shader.PropertyToID("_DstBlendA");

        private static readonly Color DefBorder = Hex("#FF8158");
        // Matches the shader's own Properties-block defaults (StudioCardFrame.shader), ported 1:1
        // from Explorer's BackgroundLoading.mat.
        private static readonly Color DefDclInner = Hex("#BF00FF");
        private static readonly Color DefDclOuter = Hex("#4D0080");
        // The shader's own _DclGlowColor / _DclGlowRadius defaults. The reference declares the radius
        // as (0.05, -0.13); the sign is meaningless there (the delta is divided by it and then
        // length()'d), so it is stored positive to keep the sliders sane.
        private static readonly Color DefGlowColor = new(0.66f, 0f, 0.745f, 1f);
        private static readonly Vector2 DefGlowRadius = new(0.05f, 0.13f);

        // A zero radius divides by zero in the shader, so clamp rather than trusting the UI's range.
        private const float MIN_GLOW_RADIUS = 1e-4f;

        private static GameObject _root;
        private static Renderer _card, _fade, _mask, _border;
        private static float _borderOversize = 1f; // set by Layout(), consumed by PushParams()
        private static float _frameAspect = 1f;    // ditto — the only place the capture's aspect is used
        private static double _nextCheck;
        private static bool _warnedMissingShader;

        static StudioCardFrame()
        {
            EditorApplication.update += Update;
        }

        // --- Persisted properties (setters push immediately, like the shader switcher) -----------

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(K_ENABLED, false);
            set { EditorPrefs.SetBool(K_ENABLED, value); Refresh(); }
        }

        /// <summary>Hide the middle card panel, its border, and the bottom fade, leaving just the
        /// avatar over the empty (transparent) frame. Off by default. Switching it ON also clears
        /// <see cref="SideMask"/> and <see cref="BottomMask"/> — cropping the avatar to a card that
        /// isn't drawn is never what you want, and the crop is invisible until you export.</summary>
        public static bool DisableMiddleCard
        {
            get => EditorPrefs.GetBool(K_DISABLE_MIDDLE_CARD, false);
            set
            {
                EditorPrefs.SetBool(K_DISABLE_MIDDLE_CARD, value);
                // Written straight to the prefs rather than through the properties so this is one
                // Refresh(), not three. Deliberately ONE-WAY: turning the card back on doesn't silently
                // re-enable the masks (that would need remembered hidden state), you re-tick them.
                if (value)
                {
                    EditorPrefs.SetBool(K_SIDEMASK, false);
                    EditorPrefs.SetBool(K_BOTTOMMASK, false);
                }
                Refresh();
            }
        }

        private static Texture2D _defaultPattern;

        private static Texture2D DefaultPattern =>
            _defaultPattern ??= AssetDatabase.LoadAssetAtPath<Texture2D>(DCL_BG_TEXTURE_PATH);

        /// <summary>
        /// The tiling pattern sampled over the card's vignette. Defaults to the bundled
        /// <c>DclBackgroundPattern.png</c> (Explorer's icon atlas) whenever no custom asset is chosen;
        /// point it at any other texture to re-skin the card. Tile it with Wrap Mode = Repeat, or it'll
        /// clamp into streaks at the card edges. Whether the pattern draws at all is
        /// <see cref="PatternEnabled"/> — setting this to null (e.g. the Pattern field's "None") turns
        /// the pattern off rather than reverting to the bundled default; assign an asset to turn it back
        /// on.
        /// </summary>
        public static Texture2D PatternTexture
        {
            get
            {
                var guid = EditorPrefs.GetString(K_PATTERN, null);
                if (!string.IsNullOrEmpty(guid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex != null) return tex;
                    }
                }
                return DefaultPattern;
            }
            set
            {
                // Store the GUID, not the path, so moving or renaming the asset doesn't break the
                // reference. A null (or a non-asset texture, e.g. a built-in) clears back to the default
                // texture reference AND switches the pattern off — see PatternEnabled.
                var guid = "";
                if (value != null)
                {
                    var path = AssetDatabase.GetAssetPath(value);
                    if (!string.IsNullOrEmpty(path)) guid = AssetDatabase.AssetPathToGUID(path);
                }
                EditorPrefs.SetString(K_PATTERN, guid);
                PatternEnabled = value != null;
            }
        }

        /// <summary>
        /// Whether the pattern draws at all — the shader gate that makes "no pattern" (just the
        /// inner/outer vignette colours) expressible, since the pattern's luminosity blend has no
        /// neutral texture value. On by default so a fresh install still shows the bundled pattern.
        /// </summary>
        public static bool PatternEnabled
        {
            get => EditorPrefs.GetBool(K_PATTERN_ENABLED, true);
            set { EditorPrefs.SetBool(K_PATTERN_ENABLED, value); Refresh(); }
        }

        /// <summary>The two colours the card's radial vignette blends between (inner → outer). These
        /// are the card's colours — the pattern and the glow have their own knobs, and the rest of the
        /// Decentraland paint (tiling, speed) is fixed at the values ported from the reference
        /// material.</summary>
        public static Color DclInnerColor { get => GetColor(K_DCL_INNER, DefDclInner); set => SetColor(K_DCL_INNER, value); }
        public static Color DclOuterColor { get => GetColor(K_DCL_OUTER, DefDclOuter); set => SetColor(K_DCL_OUTER, value); }

        /// <summary>
        /// The radial hotspot the card adds behind the avatar. It is ADDED on top of the inner/outer
        /// vignette, so it survives blacking both of those out — which is why it needed its own knob.
        /// Alpha is a strength multiplier in the shader (<c>_DclGlowColor.a</c>), so alpha 0 turns the
        /// glow off completely while leaving the rest of the paint alone.
        /// </summary>
        public static Color GlowColor
        {
            get => GetColor(K_GLOW_COLOR, DefGlowColor, keepAlpha: true);
            set => SetColor(K_GLOW_COLOR, value);
        }

        /// <summary>
        /// The glow's horizontal and vertical radius, in the CARD's 0..1 UV space (not pixels, unlike
        /// the margins below — the shader's glow works in card UV). The stock (0.05, 0.13) is the
        /// narrow, tall ellipse that reads as a column of light behind the avatar.
        /// </summary>
        public static Vector2 GlowRadius
        {
            get => new(EditorPrefs.GetFloat(K_GLOW_RADIUS_X, DefGlowRadius.x),
                EditorPrefs.GetFloat(K_GLOW_RADIUS_Y, DefGlowRadius.y));
            set
            {
                EditorPrefs.SetFloat(K_GLOW_RADIUS_X, Mathf.Max(MIN_GLOW_RADIUS, value.x));
                EditorPrefs.SetFloat(K_GLOW_RADIUS_Y, Mathf.Max(MIN_GLOW_RADIUS, value.y));
                Refresh();
            }
        }

        /// <summary>Erase the avatar where it spills past the card's left/right edges (arms/hands),
        /// leaving the top open so the head still overflows. On by default. Independent of
        /// <see cref="BottomMask"/> — both feed the same quad, which erases outside whichever edges
        /// are switched on.</summary>
        public static bool SideMask
        {
            get => EditorPrefs.GetBool(K_SIDEMASK, true);
            set { EditorPrefs.SetBool(K_SIDEMASK, value); Refresh(); }
        }

        /// <summary>Erase the avatar where it hangs below the card's bottom edge (feet/shoes on a
        /// tall pose). On by default — a subject poking out of the bottom of the card reads as a bug,
        /// unlike the head overflowing the top, which is the intended card look.</summary>
        public static bool BottomMask
        {
            get => EditorPrefs.GetBool(K_BOTTOMMASK, true);
            set { EditorPrefs.SetBool(K_BOTTOMMASK, value); Refresh(); }
        }

        /// <summary>Close the card off at the top: the border ring runs the whole way round instead of
        /// fading out over the top 12%, and the mask crops the top edge like any other. Off by default,
        /// because the open top is deliberate for avatars — the head is meant to overflow. Turn it on
        /// for Single-Item shots, where the subject belongs fully inside the rounded rect.</summary>
        public static bool ClosedBorder
        {
            get => EditorPrefs.GetBool(K_CLOSED_BORDER, false);
            set { EditorPrefs.SetBool(K_CLOSED_BORDER, value); Refresh(); }
        }

        /// <summary>Suppress the avatar's outline (a thin silhouette line, visible over the head
        /// against a light card) for clean beauty shots. Drives <see cref="Loading.AvatarLoader"/>'s
        /// runtime flag; independent of <see cref="Enabled"/> so it works with or without the frame.
        /// Deliberately NOT persisted to EditorPrefs (unlike the other card settings) — it always
        /// starts off on a fresh domain reload/Editor launch, so it can never silently stay on across
        /// a session and leave someone wondering why the outline is missing.</summary>
        public static bool HideOutline
        {
            get => _hideOutline;
            set { _hideOutline = value; SyncOutline(); }
        }

        private static bool _hideOutline;

        /// <summary>
        /// Live override for the studio camera's post-process antialiasing mode, so SMAA's edge
        /// erosion of the (thin) outline stroke can be compared against None/FXAA/TAA live. Null =
        /// leave the camera at whatever the scene/prefab has configured. Only settable in play mode
        /// (that's when the actual rendering camera exists); not persisted. (Outline width lives in
        /// StudioAvatarShaderSwitcher's knob list, the single owner of _Outline_Width.)
        /// </summary>
        public static AntialiasingMode? DebugAntialiasing
        {
            get => _debugAntialiasing;
            set { _debugAntialiasing = value; SyncDebugOverrides(); }
        }

        private static AntialiasingMode? _debugAntialiasing;
        private static AntialiasingMode? _originalAntialiasing; // captured on first override, restored when cleared

        /// <summary>Re-applies the antialiasing override. Called from the poll so it survives a
        /// play-mode re-entry (which would otherwise reset the camera to its authored default).</summary>
        private static void SyncDebugOverrides()
        {
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH) return;

            // The antialiasing mode lives on the actual rendering camera, which only exists once play
            // mode spins up the scene for real (edit-mode preview renders through the Scene View).
            if (!Application.isPlaying) return;

            var cam = FindCamera();
            var camData = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            if (camData == null) return;

            if (_debugAntialiasing.HasValue)
            {
                _originalAntialiasing ??= camData.antialiasing;
                camData.antialiasing = _debugAntialiasing.Value;
            }
            else if (_originalAntialiasing.HasValue)
            {
                camData.antialiasing = _originalAntialiasing.Value;
                _originalAntialiasing = null;
            }
        }

        private static CameraClearFlags? _originalClearFlags; // captured on first override, restored when the frame is switched off
        private static Color _originalClearColor;

        /// <summary>
        /// With the background layer gone, "outside the card" is just the camera's clear — so while the
        /// frame is on, clear to transparent black. That makes the live view match the export (which
        /// OutfitCapture already forces to a transparent clear) instead of showing the scene camera's
        /// authored purple behind the card. Play mode only: in edit mode the preview renders through
        /// the Scene view (which owns its own background) and writing to the scene camera there would
        /// dirty the scene. Restored as soon as the frame is disabled or the studio scene is left.
        /// </summary>
        private static void SyncCameraClear()
        {
            if (!Application.isPlaying) return;

            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            var cam = inStudio ? FindCamera() : null;
            if (cam == null) return;

            if (inStudio && Enabled)
            {
                if (!_originalClearFlags.HasValue)
                {
                    _originalClearFlags = cam.clearFlags;
                    _originalClearColor = cam.backgroundColor;
                }
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
            else if (_originalClearFlags.HasValue)
            {
                cam.clearFlags = _originalClearFlags.Value;
                cam.backgroundColor = _originalClearColor;
                _originalClearFlags = null;
            }
        }

        // Defaults below are the look Mauricio dialled in on 2026-07-30 (see IMPLEMENTATION.md §18), not
        // the original 2026-07-17 ones.
        //
        // EVERY value here is a fraction of the CAPTURE (never of the card), which is what lets the window
        // show them all as pixels: px = fraction × capture width/height, so a size in px is stable when the
        // card is stretched and scales proportionally when the capture resolution changes. §20 has the
        // conversion table; PushParams() restates the card-relative ones for the shader.
        /// <summary>The card's width, as a fraction of the frame HEIGHT (not width — see Layout()). With
        /// the height set by the top/bottom margins, this fixes the card's aspect, so the whole card is
        /// invariant to the capture's aspect ratio. Default 0.55 against the default margins gives a card
        /// aspect of 0.55/0.83 ≈ 0.66, the portrait item-card shape the frame was designed around.</summary>
        public static float CardWidth { get => EditorPrefs.GetFloat(K_CARD_WIDTH, 0.55f); set => SetFloat(K_CARD_WIDTH, value); }

        public static float MarginTop { get => EditorPrefs.GetFloat(K_MARGIN_TOP, 0.12f); set => SetFloat(K_MARGIN_TOP, value); }
        public static float MarginBottom { get => EditorPrefs.GetFloat(K_MARGIN_BOTTOM, 0.05f); set => SetFloat(K_MARGIN_BOTTOM, value); }
        public static Color Border { get => GetColor(K_BORDER, DefBorder); set => SetColor(K_BORDER, value); }

        // Fractions of the frame HEIGHT. Defaults are the old card-relative values restated for the new
        // unit at the default margins (cardHFrac 0.83), so the stock look is pixel-identical to before:
        // radius 0.08 × 0.83/2 = 0.0332, inner border 0.008 × 0.83/2 = 0.00332, fade 0.2 × 0.83 = 0.166.
        public static float CornerRadius { get => EditorPrefs.GetFloat(K_RADIUS, 0.0332f); set => SetFloat(K_RADIUS, value); }
        public static float InnerBorderWidth { get => EditorPrefs.GetFloat(K_INNER_BORDER_W, 0.00332f); set => SetFloat(K_INNER_BORDER_W, value); }
        public static float OuterBorderWidth { get => EditorPrefs.GetFloat(K_OUTER_BORDER_W, 0f); set => SetFloat(K_OUTER_BORDER_W, value); }
        public static float FadeHeight { get => EditorPrefs.GetFloat(K_FADE_H, 0.166f); set => SetFloat(K_FADE_H, value); }

        public static float FadeSoftness { get => EditorPrefs.GetFloat(K_FADE_S, 0.7f); set => SetFloat(K_FADE_S, value); }

        /// <summary>The card's height as a fraction of the frame's. Shared by the pattern's tile scale, the
        /// px conversions in the window, and the clamps below — one definition so they can't drift.</summary>
        public static float CardHeightFraction => Mathf.Max(0.01f, 1f - MarginTop - MarginBottom);

        // The three card-relative knobs as PushParams will actually use them: clamped in frame-height terms
        // BEFORE the conversion to the shader's units (see PushParams for why that ordering matters).
        // Exposed because the window reports the card's painted footprint, which the outer ring extends.
        internal static float EffectiveCornerRadius => Mathf.Min(CornerRadius, 0.25f * CardHeightFraction);

        internal static float EffectiveInnerBorderWidth =>
            Mathf.Min(InnerBorderWidth, MAX_BORDER_WIDTH * CardHeightFraction * 0.5f);

        internal static float EffectiveOuterBorderWidth =>
            Mathf.Min(OuterBorderWidth, MAX_BORDER_WIDTH * CardHeightFraction * 0.5f);

        public static void ResetDefaults()
        {
            foreach (var k in new[]
                     {
                         K_CARD_WIDTH, K_MARGIN_TOP, K_MARGIN_BOTTOM, K_RADIUS, K_BORDER, K_INNER_BORDER_W,
                         K_OUTER_BORDER_W, K_FADE_H, K_FADE_S, K_DCL_INNER, K_DCL_OUTER, K_PATTERN,
                         K_PATTERN_ENABLED, K_GLOW_COLOR, K_GLOW_RADIUS_X, K_GLOW_RADIUS_Y
                     })
                EditorPrefs.DeleteKey(k);
            Refresh();
        }

        // --- Poll + refresh ----------------------------------------------------------------------

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + 0.5;
            SyncOutline();
            SyncDebugOverrides();
            SyncCameraClear();
            Refresh();
        }

        /// <summary>
        /// Push the outline-suppression flag onto the runtime loaders. Only overrides inside the
        /// studio scene, so the outline behaves normally in the main app / other scenes. Re-applied
        /// every poll tick so it survives a domain reload or entering play mode (where the static
        /// resets). The flag is only read while playing (the outline renders in play mode).
        /// </summary>
        private static void SyncOutline()
        {
            // Suppress only while the studio window is open in the studio scene, so closing the
            // window or leaving the scene auto-restores the outline (the poll runs every tick, so a
            // stale "on" preference can never leave the outline stuck off with no visible control).
            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            var windowOpen = EditorWindow.HasOpenInstances<OutfitStudioWindow>();
            Loading.AvatarLoader.OutlineSuppressed = inStudio && windowOpen && HideOutline;
        }

        /// <summary>Ensure/teardown the quads and push the current settings. Cheap and idempotent.</summary>
        public static void Refresh()
        {
            var inStudio = SceneManager.GetActiveScene().path == OutfitStudioWindow.STUDIO_SCENE_PATH;
            if (!inStudio || !Enabled)
            {
                Teardown();
                return;
            }

            var cam = FindCamera();
            if (cam == null) { Teardown(); return; }

            if (_root == null && !TryReattach())
            {
                if (!Create(cam)) return; // shader missing — warned once inside Create
            }

            // Keep the frame parented to whatever camera renders now (it can change across play mode).
            if (_root.transform.parent != cam.transform)
                _root.transform.SetParent(cam.transform, false);

            Layout(cam);
            PushParams();
        }

        /// <summary>
        /// Re-lays-out for a specific camera/aspect — called by OutfitCapture right before a still so
        /// the card matches the capture resolution even if it differs from the Game view aspect.
        /// </summary>
        public static void RelayoutFor(Camera cam)
        {
            if (_root == null || cam == null) return;
            Layout(cam);
            PushParams(); // keep _CardAspect/_CornerRadius in sync with the capture's aspect, not the
                           // Game view's — otherwise the rounded-corner/border math is evaluated for
                           // the wrong aspect and a seam opens between quads only in the capture.
        }

        public static bool IsActive => _root != null;

        private static void Teardown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            _card = _fade = _mask = _border = null;
        }

        private static bool TryReattach()
        {
            // After a domain reload the static refs are gone but a DontSave root may survive; find it.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name != ROOT_NAME) continue;
                if (go.scene != SceneManager.GetActiveScene()) continue;
                _root = go;
                _card = FindChild("Card");
                _fade = FindChild("Fade");
                _mask = FindChild("Mask");
                _border = FindChild("Border");
                if (_card != null && _fade != null && _mask != null && _border != null)
                    return true;
                Object.DestroyImmediate(go); // malformed — rebuild from scratch
                _root = null;
                return false;
            }
            return false;
        }

        private static Renderer FindChild(string n)
        {
            var t = _root.transform.Find(n);
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        private static bool Create(Camera cam)
        {
            var shader = Shader.Find(SHADER_NAME);
            if (shader == null)
            {
                if (!_warnedMissingShader)
                {
                    Debug.LogWarning($"[OutfitStudio] Shader \"{SHADER_NAME}\" not found — card frame skipped. " +
                                     "Check the Console for compile errors or reimport Assets/OutfitStudio/Shaders.");
                    _warnedMissingShader = true;
                }
                return false;
            }
            _warnedMissingShader = false;

            _root = new GameObject(ROOT_NAME) { hideFlags = HideFlags.DontSave };
            _root.transform.SetParent(cam.transform, false);

            // queue, mode, and render state per layer.
            //
            // Card: ZWrite On, because it's the only opaque-queue layer left after the background quad
            // was dropped — without a depth write the skybox (drawn after the opaque queue) paints
            // straight over it wherever the view clears to Skybox. The shader clips the card's fully
            // transparent pixels so only the rounded rect writes depth.
            //
            // ZTest LessEqual (not Always): the card sits behind the avatar (far Z), so it must respect
            // depth. The avatar outline draws BeforeRenderingOpaques and writes near depth in its ring;
            // with ZTest Always the card painted over that ring (the outline showed the card colour).
            // LessEqual leaves the nearer outline ring — and the opaque avatar — untouched.
            _card = MakeQuad("Card", shader, mode: 0, queue: 1500,
                zTest: (int)CompareFunction.LessEqual, zWrite: 1,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            // Fade and Mask both ZWrite On (2026-08-06), which reads oddly for two layers drawn after the
            // avatar — the point is not occlusion but **depth honesty for the border below**. Each repaints
            // or erases avatar pixels without clearing the depth those pixels wrote, and the border is now
            // depth-tested, so stale depth punched gaps in the ring. Both shader paths clip the fragments
            // they don't actually cover, so the depth reset lands exactly on the region they do.
            _fade = MakeQuad("Fade", shader, mode: 1, queue: 3500,
                zTest: (int)CompareFunction.Always, zWrite: 1,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            // Side mask sits in front of the avatar (queue 3200, after opaque + transparent wearables)
            // but before the bottom fade and border, so it can't erase either of those. Zero /
            // OneMinusSrcAlpha on BOTH pairs makes it an eraser: dst *= (1 - srcAlpha), so colour and
            // alpha both go to 0 outside the card instead of being repainted (there's no background
            // to repaint with any more). Only enabled when SideMask is on.
            _mask = MakeQuad("Mask", shader, mode: 2, queue: 3200,
                zTest: (int)CompareFunction.Always, zWrite: 1,
                src: (int)BlendMode.Zero, dst: (int)BlendMode.OneMinusSrcAlpha,
                srcA: (int)BlendMode.Zero, dstA: (int)BlendMode.OneMinusSrcAlpha);
            // Border keeps the LAST queue (4000) so it still wins over the bottom fade and the side mask —
            // but ZTest LessEqual, not Always, so the **avatar occludes it** like it occludes the card
            // (Mauricio, 2026-08-06: "card outline is rendering over the wearables, should be behind like
            // the card background"). The quad already sits at PLANE_Z, far behind a ~2 m avatar; Always was
            // the only reason it floated on top. Queue and depth are independent here, which is what lets
            // the border go behind the avatar without also falling behind the fade drawn at 3500.
            _border = MakeQuad("Border", shader, mode: 3, queue: 4000,
                zTest: (int)CompareFunction.LessEqual, zWrite: 0,
                src: (int)BlendMode.SrcAlpha, dst: (int)BlendMode.OneMinusSrcAlpha);
            return true;
        }

        private static Renderer MakeQuad(string name, Shader shader, float mode, int queue,
            int zTest, int zWrite, int src, int dst,
            int srcA = (int)BlendMode.One, int dstA = (int)BlendMode.OneMinusSrcAlpha)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(_root.transform, false);

            var mat = new Material(shader) { hideFlags = HideFlags.DontSave, renderQueue = queue };
            mat.SetFloat(ModeId, mode);
            mat.SetFloat(ZTestId, zTest);
            mat.SetFloat(ZWriteId, zWrite);
            mat.SetFloat(SrcBlendId, src);
            mat.SetFloat(DstBlendId, dst);
            mat.SetFloat(SrcBlendAId, srcA);
            mat.SetFloat(DstBlendAId, dstA);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // --- Geometry & params -------------------------------------------------------------------

        private static void Layout(Camera cam)
        {
            // Frustum extents at PLANE_Z (vertical FOV; aspect from the camera = capture/Game view).
            var h = 2f * PLANE_Z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var w = h * cam.aspect;

            // Side mask covers the whole frame (slightly oversized to hide any edge sliver on aspect
            // mismatch); the card rect is passed to it as _MaskRect in this quad's UV space.
            _mask.transform.localScale = new Vector3(w * MASK_OVERSIZE, h * MASK_OVERSIZE, 1f);
            _mask.transform.localPosition = new Vector3(0f, 0f, PLANE_Z);
            _mask.transform.localRotation = Quaternion.identity;

            _frameAspect = cam.aspect; // stashed for PushParams (the mask rect is in viewport fractions)

            // The card's width is a fraction of the frame's HEIGHT, not its width — deliberately, and it's
            // the whole reason the card survives a resolution change (§20). The camera frames the avatar
            // by its VERTICAL fov, so the avatar's on-screen size only ever tracks the frame height; a
            // width-relative card would therefore change shape, and change size relative to the avatar,
            // every time the aspect changed. Height-relative on both axes means an aspect change only
            // alters how much empty space sits either side of the card, which is what an artist expects.
            float mT = MarginTop, mB = MarginBottom;
            var cw = h * Mathf.Max(0.01f, CardWidth);
            var ch = h * Mathf.Max(0.01f, 1f - mT - mB);
            var cx = 0f;                                    // horizontally centred
            var cy = ((mB + (1f - mT)) * 0.5f - 0.5f) * h;   // vertical offset of the card centre

            foreach (var r in new[] { _card, _fade })
            {
                r.transform.localScale = new Vector3(cw, ch, 1f);
                r.transform.localPosition = new Vector3(cx, cy, PLANE_Z);
                r.transform.localRotation = Quaternion.identity;
            }

            // The border quad is scaled up beyond the card so the shader (mode 3) has physical room
            // to paint the outer ring past the card edge; it remaps its raw UV back into the card's
            // normalized SDF space by this same factor (see _BorderOversize in the shader). Derived
            // from the card's own aspect: in RoundedBoxSDF's normalized space the box half-extents
            // are (aspect, 1), so the tightest reach direction — straight out from a flat edge — is
            // whichever of those two is smaller; the oversize must clear the slider's max width in
            // that direction, plus a little slack for the AA smoothstep band.
            var cardAspect = cw / Mathf.Max(ch, 1e-4f);
            var minExtent = Mathf.Max(Mathf.Min(cardAspect, 1f), 0.01f);
            _borderOversize = 1f + (MAX_BORDER_WIDTH + BORDER_OVERSIZE_MARGIN) / minExtent;
            _border.transform.localScale = new Vector3(cw * _borderOversize, ch * _borderOversize, 1f);
            _border.transform.localPosition = new Vector3(cx, cy, PLANE_Z);
            _border.transform.localRotation = Quaternion.identity;
        }

        private static void PushParams()
        {
            var cardAspect = AspectOf(_card); // cw / ch

            // The card's height as a fraction of the frame's. Used for the pattern's tile scale (keeps its
            // on-screen icon size put as the margins change — see DclCardPaint) and the conversions below.
            var cardHFrac = CardHeightFraction;

            // Corner radius and the border widths are STORED as fractions of the frame HEIGHT, so a given
            // pixel size survives the card being stretched either way (§20). RoundedBoxSDF, though, works
            // in a space normalized to the *card's* HALF-height — hence × 2 / cardHFrac.
            //
            // Clamping happens in frame-height terms, BEFORE the conversion (in the Effective* properties
            // above), so that every quad derives its own value from the same clamped physical size and they
            // can't disagree: the card and the mask must round their corners identically or the mask's edge
            // stops landing on the card's. A radius past SDF 0.5 is meaningless (RoundedBoxSDF clamps it to
            // the box anyway) and a border past MAX_BORDER_WIDTH would overflow the border quad, whose
            // oversize is sized for exactly that maximum (see Layout()). At sane margins neither clamp is
            // reachable; they only bite once the card is squashed to a sliver.
            var radiusFH = EffectiveCornerRadius;  // → SDF ≤ 0.5
            var innerFH = EffectiveInnerBorderWidth; // → SDF ≤ MAX_BORDER_WIDTH
            var outerFH = EffectiveOuterBorderWidth;

            // Frame-height fraction → RoundedBoxSDF units, for a rect of the given height (the card for
            // most quads; the extended keep-rect for the mask).
            float ToSdf(float frameHFraction, float rectHFrac) => frameHFraction * 2f / rectHFrac;

            var cornerSdf = ToSdf(radiusFH, cardHFrac);

            _card.enabled = !DisableMiddleCard;
            var card = _card.sharedMaterial;
            PushCardPaint(card, cardAspect, cardHFrac);
            card.SetFloat(CornerRadiusId, cornerSdf);

            // DCL_Emotes' "Outline As Mask" inverts the depth relationship set up in MakeQuad, and it
            // is the only thing that can: the outline ring is a DEPTH stamp laid down before the
            // opaque queue, so the only way the card appears inside it is to stop respecting depth.
            // That is precisely the ZTest Always behaviour MakeQuad's comment records as a bug —
            // "the outline showed the card colour" — which is the whole point here.
            //
            // ZWrite has to go off with it, or the card's own depth would overwrite the stamp and the
            // avatar would paint back over the ring, leaving only the outer silhouette cut. Dropping
            // it is safe exactly when it matters: the anti-skybox reason for ZWrite On doesn't apply
            // while the frame is on, since that's when we drive the camera to a SolidColor clear.
            var outlineMask = StudioAvatarShaderSwitcher.EmotesOutlineMask;
            card.SetFloat(ZTestId, (float)(int)(outlineMask ? CompareFunction.Always : CompareFunction.LessEqual));
            card.SetFloat(ZWriteId, outlineMask ? 0f : 1f);

            // Border is its own top-most quad (drawn over the avatar/fade/mask), not baked into the card.
            _border.enabled = !DisableMiddleCard;
            var border = _border.sharedMaterial;
            border.SetFloat(CardAspectId, cardAspect);
            border.SetFloat(CornerRadiusId, cornerSdf);
            border.SetColor(BorderColorId, Border);
            border.SetFloat(InnerBorderWidthId, ToSdf(innerFH, cardHFrac));
            border.SetFloat(OuterBorderWidthId, ToSdf(outerFH, cardHFrac));
            border.SetFloat(BorderOversizeId, _borderOversize);
            // Normally faded out over the top 12% so an avatar's head can overflow without the border
            // crossing it. An item card has no head to make room for and wants a closed rounded rect,
            // so 1 = no fade at all.
            border.SetFloat(BorderTopFadeId, ClosedBorder ? 1f : 0.88f);

            // The fade shares the card's transform and gets the same paint inputs, so its colour is a
            // pixel-exact continuation of the card's — it only adds the vertical alpha ramp.
            _fade.enabled = !DisableMiddleCard;
            var fade = _fade.sharedMaterial;
            PushCardPaint(fade, cardAspect, cardHFrac);
            fade.SetFloat(CornerRadiusId, cornerSdf);
            // FadeHeight is a frame-height fraction too, but the shader ramps over the card's own uv.y —
            // so divide by the card's height rather than doubling. Clamped at 1 = the whole card.
            var end = Mathf.Clamp01(FadeHeight / cardHFrac);
            fade.SetFloat(FadeEndId, end);
            fade.SetFloat(FadeStartId, end * (1f - Mathf.Clamp01(FadeSoftness)));

            // Side/bottom mask: geometry only (it erases, it doesn't paint). The shader keeps whatever
            // is inside the rect it's handed and erases the rest, so the two toggles are expressed by
            // building that rect: start from the card, then push the edges we're NOT masking far out of
            // frame so the SDF never cuts there. The top edge is always the card's — the head-overflow
            // column in the shader owns that one.
            _mask.enabled = SideMask || BottomMask;
            if (_mask.enabled)
            {
                var mask = _mask.sharedMaterial;

                // The mask quad spans the frame, so its rect is in viewport fractions of each axis. The
                // card's width is stored in frame-HEIGHT units, so converting it to a width fraction is
                // where the frame aspect (and only here) comes back in; the card is centred.
                var cardWFrac = Mathf.Max(0.01f, CardWidth) / Mathf.Max(_frameAspect, 1e-4f);
                float l = 0.5f - cardWFrac * 0.5f, r = 0.5f + cardWFrac * 0.5f;
                float b = MarginBottom, t = 1f - MarginTop;
                if (!SideMask) { l = -0.5f; r = 1.5f; }
                if (!BottomMask) b = -0.5f;

                float effW = r - l, effH = t - b;
                // Aspect is relative to whatever rect the SDF works on, so it's restated for the extended
                // keep-rect: its two viewport fractions times the frame's own aspect. The radius needs no
                // special case at all now that it's stored in frame-height terms — feeding the extended
                // rect's height to the same ToSdf() gives the card's *physical* corner size, which is what
                // keeps the mask's edge landing exactly on the card's own AA edge where the two coincide.
                mask.SetFloat(CardAspectId, _frameAspect * (effW / effH));
                mask.SetFloat(CornerRadiusId, ToSdf(radiusFH, effH));

                float U(float f) => 0.5f + (f - 0.5f) / MASK_OVERSIZE; // viewport fraction → mask-quad UV
                mask.SetVector(MaskRectId, new Vector4(U(l), U(r), U(b), U(t)));
                mask.SetFloat(MaskTopOpenId, ClosedBorder ? 0f : 1f);
            }

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        /// <summary>The Decentraland card paint inputs, shared by the card and the bottom fade.</summary>
        private static void PushCardPaint(Material mat, float cardAspect, float tileScale)
        {
            var pattern = PatternTexture;
            if (pattern != null) mat.SetTexture(DclOverlayTexId, pattern);
            mat.SetFloat(DclPatternEnabledId, PatternEnabled ? 1f : 0f);
            mat.SetColor(DclInnerColorId, DclInnerColor);
            mat.SetColor(DclOuterColorId, DclOuterColor);
            mat.SetColor(DclGlowColorId, GlowColor);
            mat.SetVector(DclGlowRadiusId, GlowRadius);
            mat.SetFloat(DclTileScaleId, tileScale);
            mat.SetFloat(CardAspectId, cardAspect);
        }

        private static float AspectOf(Renderer r)
        {
            var s = r.transform.localScale;
            return s.y > 1e-4f ? s.x / s.y : 0.66f;
        }

        // internal (not private): reused by StudioFlyCameraController, which needs the exact same
        // "which camera is actually live" resolution — the studio scene can have more than one
        // GameObject tagged MainCamera (e.g. before the Configurator camera is stripped per §14), so
        // Camera.main alone isn't reliable there.
        internal static Camera FindCamera()
        {
            // Parent to the same camera OutfitCapture renders (Camera.main) so the quads stay aligned
            // in the capture. Fall back to the studio PreviewCamera / highest-depth enabled camera
            // (e.g. before the Configurator camera is stripped per §14, or if the tag is missing).
            var scene = SceneManager.GetActiveScene();
            var main = Camera.main;
            if (main != null && main.gameObject.scene == scene && main.isActiveAndEnabled) return main;

            Camera best = null;
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (cam.gameObject.scene != scene || !cam.isActiveAndEnabled) continue;
                if (cam.name == "PreviewCamera") return cam;
                if (best == null || cam.depth > best.depth) best = cam;
            }
            return best;
        }

        // --- EditorPrefs helpers -----------------------------------------------------------------

        private static Color Hex(string s) => ColorUtility.TryParseHtmlString(s, out var c) ? c : Color.magenta;

        private static Color GetColor(string key, Color def, bool keepAlpha = false)
        {
            var s = EditorPrefs.GetString(key, null);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c))
                return keepAlpha ? c : new Color(c.r, c.g, c.b, def.a);
            return def;
        }

        private static void SetColor(string key, Color value)
        {
            EditorPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(value));
            Refresh();
        }

        private static void SetFloat(string key, float value)
        {
            EditorPrefs.SetFloat(key, value);
            Refresh();
        }
    }
}
