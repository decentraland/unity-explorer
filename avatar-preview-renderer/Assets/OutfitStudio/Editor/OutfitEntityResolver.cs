using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Loading;
using Services;
using UnityEngine;
using Utils;

namespace OutfitStudio.Editor
{
    /// <summary>
    /// Turns an <see cref="OutfitDefinition"/> into the resolved entity list the hiding rules and the
    /// loaders both work from: catalog URNs fetched (and cached) through <see cref="EntityService"/>,
    /// draft base64 items parsed, one item per slot with drafts winning, invalid body-shape
    /// representations dropped, and the body entity prepended.
    ///
    /// Shared by <see cref="EditModeAvatarPreview"/> and <see cref="OutfitHidingReport"/> so the slot
    /// dedup rules (which mirror PreviewController.LoadForBuilder) exist in exactly one place — a
    /// second copy would let the badges disagree with what actually renders.
    /// </summary>
    public static class OutfitEntityResolver
    {
        public readonly struct Resolved
        {
            /// <summary>The body entity followed by one wearable per occupied slot.</summary>
            public readonly List<EntityDefinition> Definitions;

            public readonly BodyShape BodyShape;

            /// <summary>URNs the catalyst had nothing for (third-party/linked wearables).</summary>
            public readonly List<string> Unresolved;

            public Resolved(BodyShape bodyShape, List<EntityDefinition> definitions, List<string> unresolved)
            {
                BodyShape = bodyShape;
                Definitions = definitions;
                Unresolved = unresolved;
            }
        }

        /// <summary>
        /// Resolves the outfit. <paramref name="status"/> is optional — callers that aren't driving the
        /// window's status line (the hiding report re-resolves from cache on every apply) pass null
        /// rather than reporting the same skipped item twice.
        /// </summary>
        public static async Awaitable<Resolved> Resolve(OutfitDefinition outfit, Action<string, bool> status = null)
        {
            var bodyShape = outfit.bodyShape.Equals(WearablesConstants.BODY_SHAPE_FEMALE,
                StringComparison.OrdinalIgnoreCase)
                ? BodyShape.Female
                : BodyShape.Male;

            await EntityService.PreloadBodyEntities();

            // EffectiveUrns/EffectiveBase64Items rather than the raw lists, so Single-Item mode resolves
            // to just the isolated item here — which is what makes both callers (the edit-mode preview
            // and the hiding report) agree with it without either knowing the mode exists.
            var requestedUrns = outfit.EffectiveUrns().Select(URNUtils.SanitizeURN).ToArray();
            var urnEntities = await EntityService.GetEntities(requestedUrns);

            // Entities the catalyst couldn't resolve (e.g. third-party/linked wearables)
            // are skipped with a warning instead of failing the whole preview
            var resolvedUrns = urnEntities.Select(e => e.URN).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unresolved = requestedUrns.Where(urn => !resolvedUrns.Contains(urn)).ToList();
            if (unresolved.Count > 0 && status != null)
            {
                Debug.LogWarning($"[OutfitStudio] Could not resolve entities for: {string.Join(", ", unresolved)}");
            }

            // Slot dedup + body-shape validity (mirrors PreviewController.LoadForBuilder,
            // but skips invalid representations instead of letting the loader throw)
            var slots = new Dictionary<string, EntityDefinition>();
            foreach (var entity in urnEntities.Where(e => e.Type != EntityType.Emote))
            {
                if (!entity.HasRepresentation(bodyShape))
                {
                    status?.Invoke(
                        $"Skipped {entity.URN[(entity.URN.LastIndexOf(':') + 1)..]}: no {bodyShape} representation",
                        true);
                    continue;
                }

                slots[entity.Category] = entity;
            }

            // Draft (builder) items — base64 wins per category, same as LoadForBuilder.
            // Draft emotes are play-mode-only (edit mode is a static pose) and skipped here.
            foreach (var base64 in outfit.EffectiveBase64Items())
            {
                try
                {
                    var entity = EntityDefinition.FromBase64(OutfitDefinition.DecodeBase64(base64));

                    if (entity.Type == EntityType.Emote) continue;

                    if (!entity.HasRepresentation(bodyShape))
                    {
                        status?.Invoke($"Skipped draft {entity.URN}: no {bodyShape} representation", true);
                        continue;
                    }

                    slots[entity.Category] = entity;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OutfitStudio] Failed to parse draft item: {e.Message}");
                }
            }

            var definitions = new List<EntityDefinition> { EntityService.GetBodyEntity(bodyShape) };
            definitions.AddRange(slots.Values);

            return new Resolved(bodyShape, definitions, unresolved);
        }
    }
}
