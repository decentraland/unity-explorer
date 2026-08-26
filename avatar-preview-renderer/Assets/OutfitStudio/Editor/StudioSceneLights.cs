using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Live tuning for the studio scene's three lights — the directional key light and the two
    /// spotlights — persisted in EditorPrefs and pushed onto the lights from
    /// <c>EditorApplication.update</c>, the same shape <see cref="StudioCardFrame"/> uses. EditorPrefs
    /// rather than editing the scene means a tuning session doesn't have to be saved to survive a domain
    /// reload, and doesn't turn `OutfitStudio.unity` into a churn file.
    ///
    /// **Nothing is written until something is actually tuned.** <see cref="Apply"/> returns immediately
    /// while no override key exists, and each write is guarded by a comparison, so the steady state — both
    /// untouched and tuned — performs no writes at all. That matters because in edit mode any write to a
    /// light marks the scene dirty: without these guards, merely having the window open would dirty the
    /// scene every editor frame.
    ///
    /// **Studio scene only** (the same gate as `StudioCardFrame`/`StudioAvatarShaderSwitcher`). The
    /// spotlights exist only here — `Main.unity` has just a directional light — so without the gate,
    /// Reset would push studio values onto the production scene's lighting.
    /// </summary>
    [InitializeOnLoad]
    public static class StudioSceneLights
    {
        // GameObject names in OutfitStudio.unity. Matched by name because these are plain scene objects
        // with no marker component, and adding one would be a scene edit to avoid scene edits.
        private const string DIRECTIONAL = "Directional Light";
        private const string SPOT_FRONT = "Spot Light Front";
        private const string SPOT_BACK = "Spot Light Back";

        // --- Scene-authored defaults -------------------------------------------------------------
        //
        // Kept in lockstep with OutfitStudio.unity, which is what makes "Reset" mean "back to how the
        // scene ships" rather than "back to something plausible". If the scene's lighting is ever
        // re-authored, these have to be re-read — there is no way to recover them once an override is
        // applied, since the override IS what the light now holds. Re-authored together on 2026-08-06 to
        // the tuning Mauricio settled on: a much softer key (2 -> 0.6) with the gold pulled back to a pale
        // warm neutral, and the cyan rim dropped from a blown-out 31.7 to 15, leaving the front spot as
        // the dominant light. The spot colours are unchanged.
        public static readonly Color DefDirColor = new(0.8784314f, 0.85490197f, 0.7372549f); // pale warm key
        public const float DEF_DIR_INTENSITY = 0.6f;
        public const float DEF_DIR_YAW = 100f;

        public static readonly Color DefFrontColor = new(1f, 0.80784315f, 0.5803922f); // warm fill
        public const float DEF_FRONT_INTENSITY = 8f;

        public static readonly Color DefBackColor = new(0f, 0.7305918f, 1f); // cyan rim
        public const float DEF_BACK_INTENSITY = 15f;

        /// <summary>
        /// The directional light's X and Z euler, held constant so the exposed Y stays a pure yaw. Taken
        /// from the scene's <c>m_LocalEulerAnglesHint</c> (-205, 100, -37) rather than from
        /// <c>eulerAngles</c>, which would report the normalised (155, 100, 323) — the same rotation, since
        /// adding 360° to a euler component is the identity, but not the numbers in the inspector.
        /// </summary>
        private const float DIR_EULER_X = -205f;
        private const float DIR_EULER_Z = -37f;

        // --- EditorPrefs keys --------------------------------------------------------------------
        private const string K_DIR_COLOR = "OutfitStudio.Lights.DirColor";
        private const string K_DIR_INTENSITY = "OutfitStudio.Lights.DirIntensity";
        private const string K_DIR_YAW = "OutfitStudio.Lights.DirYaw";
        private const string K_FRONT_COLOR = "OutfitStudio.Lights.FrontColor";
        private const string K_FRONT_INTENSITY = "OutfitStudio.Lights.FrontIntensity";
        private const string K_BACK_COLOR = "OutfitStudio.Lights.BackColor";
        private const string K_BACK_INTENSITY = "OutfitStudio.Lights.BackIntensity";

        private static readonly string[] ALL_KEYS =
        {
            K_DIR_COLOR, K_DIR_INTENSITY, K_DIR_YAW,
            K_FRONT_COLOR, K_FRONT_INTENSITY,
            K_BACK_COLOR, K_BACK_INTENSITY
        };

        // Cached so the per-frame Apply costs one bool test instead of seven EditorPrefs lookups.
        // Invalidated by every setter and by Reset; there is no other writer.
        private static bool? _hasOverrides;

        static StudioSceneLights() => EditorApplication.update += Apply;

        // --- Persisted properties (setters push immediately, like the card frame) -----------------

        public static Color DirColor
        {
            get => GetColor(K_DIR_COLOR, DefDirColor);
            set => SetColor(K_DIR_COLOR, value);
        }

        public static float DirIntensity
        {
            get => EditorPrefs.GetFloat(K_DIR_INTENSITY, DEF_DIR_INTENSITY);
            set => SetFloat(K_DIR_INTENSITY, value);
        }

        public static float DirYaw
        {
            get => EditorPrefs.GetFloat(K_DIR_YAW, DEF_DIR_YAW);
            set => SetFloat(K_DIR_YAW, value);
        }

        public static Color FrontColor
        {
            get => GetColor(K_FRONT_COLOR, DefFrontColor);
            set => SetColor(K_FRONT_COLOR, value);
        }

        public static float FrontIntensity
        {
            get => EditorPrefs.GetFloat(K_FRONT_INTENSITY, DEF_FRONT_INTENSITY);
            set => SetFloat(K_FRONT_INTENSITY, value);
        }

        public static Color BackColor
        {
            get => GetColor(K_BACK_COLOR, DefBackColor);
            set => SetColor(K_BACK_COLOR, value);
        }

        public static float BackIntensity
        {
            get => EditorPrefs.GetFloat(K_BACK_INTENSITY, DEF_BACK_INTENSITY);
            set => SetFloat(K_BACK_INTENSITY, value);
        }

        /// <summary>Whether anything has been tuned, i.e. whether the lights are ours to drive at all.</summary>
        public static bool HasOverrides
        {
            get
            {
                if (_hasOverrides.HasValue) return _hasOverrides.Value;

                var found = false;
                foreach (var key in ALL_KEYS)
                {
                    if (!EditorPrefs.HasKey(key)) continue;
                    found = true;
                    break;
                }

                _hasOverrides = found;
                return found;
            }
        }

        /// <summary>
        /// Drops every override and puts the scene's authored values back on the lights.
        ///
        /// The explicit re-apply is the point: once the keys are gone <see cref="Apply"/> is a no-op, so
        /// without it the lights would simply keep whatever the last tuning left them at and "Reset" would
        /// appear to do nothing.
        /// </summary>
        public static void ResetToSceneDefaults()
        {
            foreach (var key in ALL_KEYS) EditorPrefs.DeleteKey(key);
            _hasOverrides = false;

            Push(force: true);
        }

        /// <summary>Per-frame re-apply, so a play-mode entry or scene reload can't strand a tuning.</summary>
        private static void Apply()
        {
            if (!HasOverrides) return;

            Push(force: false);
        }

        private static void Push(bool force)
        {
            if (SceneManager.GetActiveScene().path != OutfitStudioWindow.STUDIO_SCENE_PATH) return;
            if (!force && !HasOverrides) return;

            // Qualified: a bare `Object` is the kind of thing that goes CS0104-ambiguous the moment
            // someone adds `using System` to this file. Excludes inactive objects by default, which is
            // what we want — a disabled light isn't lighting anything.
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                switch (light.gameObject.name)
                {
                    case DIRECTIONAL:
                        ApplyColorAndIntensity(light, DirColor, DirIntensity);
                        ApplyYaw(light, DirYaw);
                        break;
                    case SPOT_FRONT:
                        ApplyColorAndIntensity(light, FrontColor, FrontIntensity);
                        break;
                    case SPOT_BACK:
                        ApplyColorAndIntensity(light, BackColor, BackIntensity);
                        break;
                }
            }
        }

        // Compare-then-write throughout: an unconditional assignment would re-dirty the scene on every
        // editor frame, which is the difference between a tuning tool and a tool that nags you to save.
        private static void ApplyColorAndIntensity(Light light, Color color, float intensity)
        {
            if (!SameRgb(light.color, color)) light.color = color;
            if (!Mathf.Approximately(light.intensity, intensity)) light.intensity = intensity;
        }

        private static void ApplyYaw(Light light, float yaw)
        {
            var target = Quaternion.Euler(DIR_EULER_X, yaw, DIR_EULER_Z);

            // Angle rather than a component compare: two different eulers can be the same rotation, and
            // quaternion sign is not unique either, so comparing numbers would write every frame.
            if (Quaternion.Angle(light.transform.rotation, target) > 0.01f)
                light.transform.rotation = target;
        }

        /// <summary>Alpha is ignored — it's meaningless on a light, and the ColorField still shows one.</summary>
        private static bool SameRgb(Color a, Color b) =>
            Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b);

        private static Color GetColor(string key, Color def)
        {
            var s = EditorPrefs.GetString(key, null);
            if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out var c))
                return new Color(c.r, c.g, c.b, def.a);
            return def;
        }

        private static void SetColor(string key, Color value)
        {
            EditorPrefs.SetString(key, "#" + ColorUtility.ToHtmlStringRGBA(value));
            _hasOverrides = true;
            Push(force: true);
        }

        private static void SetFloat(string key, float value)
        {
            EditorPrefs.SetFloat(key, value);
            _hasOverrides = true;
            Push(force: true);
        }
    }
}
