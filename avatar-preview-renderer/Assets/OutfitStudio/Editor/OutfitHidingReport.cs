using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using UnityEngine;
using Utils;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Works out which of the outfit's categories the DCL hiding rules suppress and which equipped
    /// item is responsible, for the slot list's "hidden by ..." badges.
    ///
    /// Everything is derived from the renderer's own <see cref="AvatarUtils.HideWearables"/> instead of
    /// re-reading hides/replaces here, and without any hook inside runtime code: attribution comes
    /// from re-running that function with one item left out at a time, so whatever the loader would
    /// hide is exactly what gets reported. That works because HideWearables is a pure function over
    /// resolved entity definitions — no scene state, nothing loaded — so probing it costs a handful of
    /// hash-set operations per equipped item.
    ///
    /// Computed from the outfit rather than from whatever avatar last loaded, so loading a Random
    /// Profile in play mode can't leave the badges describing someone else's wearables.
    /// </summary>
    public static class OutfitHidingReport
    {
        /// <summary>
        /// Hidden category -> the equipped category responsible, or <see cref="MULTIPLE_HIDERS"/>.
        /// Only categories the rules actually suppress appear; force-rendered ones drop out.
        /// </summary>
        public static readonly Dictionary<string, string> HiddenBy = new();

        /// <summary>
        /// Attribution for a category that stays hidden no matter which single item is removed,
        /// because two or more equipped items hide it.
        /// </summary>
        public const string MULTIPLE_HIDERS = "several items";

        /// <summary>Raised after <see cref="HiddenBy"/> is refilled.</summary>
        public static event Action Changed;

        private static int _sequence;

        /// <summary>
        /// Recomputes the report for <paramref name="outfit"/>. Safe to call on every apply — entity
        /// resolution comes back from EntityService's cache after the first pass. Stale runs are
        /// discarded, so a rapid sequence of edits reports only the newest outfit.
        /// </summary>
        public static async void Refresh(OutfitDefinition outfit)
        {
            var sequence = ++_sequence;

            try
            {
                var resolved = await OutfitEntityResolver.Resolve(outfit);

                if (sequence != _sequence) return;

                Rebuild(resolved.BodyShape, resolved.Definitions, outfit.EffectiveForceRender());
            }
            catch (Exception e)
            {
                // Badges are diagnostics — never let them break an apply
                Debug.LogWarning($"[OutfitStudio] Could not compute the hiding report: {e.Message}");
            }
        }

        private static void Rebuild(BodyShape bodyShape, List<EntityDefinition> definitions, string[] forceRender)
        {
            HiddenBy.Clear();

            var hidden = AvatarUtils.HideWearables(bodyShape, definitions, forceRender);

            if (hidden.Count > 0)
            {
                // Leave-one-out: whatever stops being hidden once an item is removed was being hidden
                // by that item. The body entity always stays — HideWearables expects it, and it isn't
                // a suspect. Removals that *add* hides (dropping a hat lets the hair it suppressed
                // start hiding something itself) are simply not matches, so they're ignored.
                foreach (var suspect in definitions.Where(d => d.Type != EntityType.Body))
                {
                    var without = definitions.Where(d => d != suspect).ToList();
                    var hiddenWithout = AvatarUtils.HideWearables(bodyShape, without, forceRender);

                    foreach (var category in hidden)
                    {
                        if (!hiddenWithout.Contains(category)) HiddenBy.TryAdd(category, suspect.Category);
                    }
                }

                // Whatever no single removal revealed is hidden by more than one item at once
                foreach (var category in hidden) HiddenBy.TryAdd(category, MULTIPLE_HIDERS);
            }

            Changed?.Invoke();
        }
    }
}
